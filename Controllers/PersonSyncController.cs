using RampaSegura.Api.Common;
using RampaSegura.Api.Models.Sync;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    /// <summary>
    /// Sincronización de la tabla person LOCAL -> NUBE, bajo demanda (incremental).
    /// Solo envía personas con is_synced = 0 (que sp_person_sync_from_ncheck marca
    /// SOLO cuando cambia un dato real) y las marca is_synced = 1 tras subirlas.
    /// </summary>
    [LocalOnly]
    [ApiController]
    [Route("api/[controller]")]
    public class PersonSyncController : ControllerBase
    {
        private const string SyncType = "PERSON";

        private readonly PersonSyncRepository _repository;
        private readonly ErrorLogRepository _errorLog;
        private readonly ILogger<PersonSyncController> _logger;

        public PersonSyncController(
            PersonSyncRepository repository,
            ErrorLogRepository errorLog,
            ILogger<PersonSyncController> logger)
        {
            _repository = repository;
            _errorLog = errorLog;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/personsync/execute
        /// 1) Lee personas pendientes (is_synced = 0) de la base local.
        /// 2) Las envía (upsert) a la nube.
        /// 3) Marca is_synced = 1 en local solo de lo enviado.
        /// 4) Registra el resultado en sync_log (local).
        /// Cualquier fallo se registra en la base de errores compartida y en sync_log,
        /// y se responde 503 con el detalle real.
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                var personas = await _repository.GetPendingLocalAsync(ct);

                if (personas.Count == 0)
                {
                    return Ok(new SyncResult
                    {
                        Status = "SUCCESS",
                        RowsSent = 0,
                        Message = "No hay personas pendientes de sincronizar."
                    });
                }

                await _repository.PushToCloudAsync(personas, ct);
                await _repository.MarkSyncedLocalAsync(personas, ct);
                await _repository.WriteSyncLogLocalAsync("SUCCESS", SyncType, personas.Count, null, ct);

                _logger.LogInformation("Sync person -> nube OK (endpoint), filas={Rows}", personas.Count);

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = personas.Count,
                    Message = $"{personas.Count} persona(s) sincronizada(s) a la nube."
                });
            }
            catch (DataAccessException ex)
            {
                return await HandleSyncFailureAsync(ex, ct);
            }
        }

        /// <summary>
        /// Registra el fallo en la base de errores compartida y en sync_log (best-effort),
        /// y arma la respuesta 503 con la causa real.
        /// </summary>
        private async Task<ActionResult<SyncResult>> HandleSyncFailureAsync(DataAccessException ex, CancellationToken ct)
        {
            var causa = ex.InnerException?.Message ?? ex.Message;

            var esSinConexion = causa.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase);
            var prefijo = esSinConexion
                ? "SIN CONEXION A INTERNET / NUBE NO DISPONIBLE: "
                : "ERROR AL SINCRONIZAR: ";

            var mensaje = prefijo + causa;
            if (mensaje.Length > 255) mensaje = mensaje.Substring(0, 255);

            _logger.LogWarning("Sync person -> nube FALLÓ (endpoint). {Mensaje}", causa);

            await _errorLog.RegisterAsync(
                $"POST /api/personsync/execute [{SyncType}]",
                StatusCodes.Status503ServiceUnavailable,
                causa,
                HttpContext.GetClientIp(),
                "SYNC");

            try
            {
                await _repository.WriteSyncLogLocalAsync("FAILED", SyncType, 0, mensaje, ct);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "No se pudo registrar el FAILED en sync_log local");
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new SyncResult
            {
                Status = "FAILED",
                RowsSent = 0,
                ErrorMessage = mensaje
            });
        }
    }
}
