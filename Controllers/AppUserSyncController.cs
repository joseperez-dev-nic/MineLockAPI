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
    /// Sincronización de app_user LOCAL -> NUBE, bajo demanda.
    /// Envía todos los usuarios (upsert por user_id).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AppUserSyncController : ControllerBase
    {
        private const string SyncType = "APP_USER";

        private readonly AppUserSyncRepository _repository;
        private readonly ErrorLogRepository _errorLog;
        private readonly ILogger<AppUserSyncController> _logger;

        public AppUserSyncController(
            AppUserSyncRepository repository,
            ErrorLogRepository errorLog,
            ILogger<AppUserSyncController> logger)
        {
            _repository = repository;
            _errorLog = errorLog;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/appusersync/execute
        /// 1) Lee todos los usuarios de la base local.
        /// 2) Los envía (upsert) a la nube.
        /// 3) Registra el resultado en sync_log (local).
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                var usuarios = await _repository.GetSourceLocalAsync(ct);

                if (usuarios.Count == 0)
                {
                    return Ok(new SyncResult
                    {
                        Status = "SUCCESS",
                        RowsSent = 0,
                        Message = "No hay usuarios para sincronizar."
                    });
                }

                await _repository.PushToCloudAsync(usuarios, ct);
                await _repository.WriteSyncLogLocalAsync("SUCCESS", SyncType, usuarios.Count, null, ct);

                _logger.LogInformation("Sync app_user -> nube OK (endpoint), filas={Rows}", usuarios.Count);

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = usuarios.Count,
                    Message = $"{usuarios.Count} usuario(s) sincronizado(s) a la nube."
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

            _logger.LogWarning("Sync app_user -> nube FALLÓ (endpoint). {Mensaje}", causa);

            await _errorLog.RegisterAsync(
                $"POST /api/appusersync/execute [{SyncType}]",
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
