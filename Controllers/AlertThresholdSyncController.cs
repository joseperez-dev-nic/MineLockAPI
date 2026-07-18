using RampaSegura.Api.Common;
using RampaSegura.Api.Models;
using RampaSegura.Api.Models.Sync;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    /// <summary>
    /// Sincronización de alert_threshold_setting LOCAL -> NUBE, bajo demanda.
    /// Envía todos los límites (upsert por setting_id). La nube los necesita porque
    /// sp_warning_report corre allá y los lee para calcular nivel_alerta.
    /// </summary>
    [Authorize(Roles = RoleCodes.Admin)]
    [ApiController]
    [Route("api/[controller]")]
    public class AlertThresholdSyncController : ControllerBase
    {
        private const string SyncType = "ALERT_THRESHOLD";

        private readonly AlertThresholdSyncRepository _repository;
        private readonly ErrorLogRepository _errorLog;
        private readonly ILogger<AlertThresholdSyncController> _logger;

        public AlertThresholdSyncController(
            AlertThresholdSyncRepository repository,
            ErrorLogRepository errorLog,
            ILogger<AlertThresholdSyncController> logger)
        {
            _repository = repository;
            _errorLog = errorLog;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/alertthresholdsync/execute
        /// 1) Lee los límites de alerta de la base local.
        /// 2) Los envía (upsert) a la nube.
        /// 3) Registra el resultado en sync_log (local).
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                var limites = await _repository.GetSourceLocalAsync(ct);

                if (limites.Count == 0)
                {
                    return Ok(new SyncResult
                    {
                        Status = "SUCCESS",
                        RowsSent = 0,
                        Message = "No hay límites de alerta para sincronizar."
                    });
                }

                await _repository.PushToCloudAsync(limites, ct);
                await _repository.WriteSyncLogLocalAsync("SUCCESS", SyncType, limites.Count, null, ct);

                _logger.LogInformation("Sync alert_threshold_setting -> nube OK (endpoint), filas={Rows}", limites.Count);

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = limites.Count,
                    Message = $"{limites.Count} límite(s) de alerta sincronizado(s) a la nube."
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

            _logger.LogWarning("Sync alert_threshold_setting -> nube FALLÓ (endpoint). {Mensaje}", causa);

            await _errorLog.RegisterAsync(
                $"POST /api/alertthresholdsync/execute [{SyncType}]",
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
