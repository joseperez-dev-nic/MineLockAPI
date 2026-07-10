using RampaSegura.Api.Common;
using RampaSegura.Api.Models;
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
    /// Sincronización de person_photo LOCAL -> NUBE, bajo demanda (incremental).
    /// Solo envía fotos con is_synced = 0 y las marca is_synced = 1 tras subirlas.
    /// Como las fotos referencian a person, conviene llamar primero /api/personsync/execute.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PhotoSyncController : ControllerBase
    {
        private const string SyncType = "PHOTO";

        private readonly PhotoSyncRepository _repository;
        private readonly ErrorLogRepository _errorLog;
        private readonly ILogger<PhotoSyncController> _logger;

        public PhotoSyncController(
            PhotoSyncRepository repository,
            ErrorLogRepository errorLog,
            ILogger<PhotoSyncController> logger)
        {
            _repository = repository;
            _errorLog = errorLog;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/photosync/execute
        /// 1) Lee fotos pendientes (is_synced = 0) de la base local.
        /// 2) Las envía (upsert) a la nube.
        /// 3) Marca is_synced = 1 en local solo de lo enviado.
        /// 4) Registra el resultado en sync_log (local).
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                var fotos = await _repository.GetPendingLocalAsync(ct);

                if (fotos.Count == 0)
                {
                    return Ok(new SyncResult
                    {
                        Status = "SUCCESS",
                        RowsSent = 0,
                        Message = "No hay fotos pendientes de sincronizar."
                    });
                }

                await _repository.PushToCloudAsync(fotos, ct);
                await _repository.MarkSyncedLocalAsync(fotos, ct);
                await _repository.WriteSyncLogLocalAsync("SUCCESS", SyncType, fotos.Count, null, ct);

                _logger.LogInformation("Sync photo -> nube OK (endpoint), filas={Rows}", fotos.Count);

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = fotos.Count,
                    Message = $"{fotos.Count} foto(s) sincronizada(s) a la nube."
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

            _logger.LogWarning("Sync photo -> nube FALLÓ (endpoint). {Mensaje}", causa);

            await _errorLog.RegisterAsync(
                $"POST /api/photosync/execute [{SyncType}]",
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
