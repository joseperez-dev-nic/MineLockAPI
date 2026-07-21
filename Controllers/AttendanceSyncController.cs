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
    /// Sincronización de attendance_session LOCAL -> NUBE, disparada bajo demanda.
    /// Se debe llamar desde el despliegue LOCAL de la API (el que tiene acceso a la
    /// base local). El despliegue en la nube simplemente no llama este endpoint.
    /// </summary>
    [LocalOnly]
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceSyncController : ControllerBase
    {
        private const string SyncType = "ATTENDANCE";

        private readonly AttendanceSyncRepository _repository;
        private readonly ErrorLogRepository _errorLog;
        private readonly ILogger<AttendanceSyncController> _logger;

        public AttendanceSyncController(
            AttendanceSyncRepository repository,
            ErrorLogRepository errorLog,
            ILogger<AttendanceSyncController> logger)
        {
            _repository = repository;
            _errorLog = errorLog;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/attendancesync/execute
        /// Ciclo BIDIRECCIONAL:
        ///   PULL (nube -> local): trae cambios hechos en la nube (p. ej. cierres
        ///     manuales), los aplica en local y los marca sincronizados en la nube.
        ///   PUSH (local -> nube): envía los marcajes/cambios de local a la nube.
        /// El PULL va primero: si un cierre manual se hizo en la nube, entra a local
        /// antes de que el push lo sobrescriba. Cada upsert pone is_synced=1 en el
        /// destino, así no hay ping-pong.
        /// Cualquier fallo se registra en la base de errores compartida y en sync_log,
        /// y se responde 503 con el detalle real.
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                // --- PULL: nube -> local (cierres manuales hechos en la nube) ---
                var fromCloud = await _repository.GetPendingCloudAsync(ct);
                if (fromCloud.Count > 0)
                {
                    await _repository.ApplyToLocalAsync(fromCloud, ct);
                    await _repository.MarkSyncedCloudAsync(fromCloud, ct);
                }

                // --- PUSH: local -> nube (marcajes y cierres hechos en local) ---
                var fromLocal = await _repository.GetPendingLocalAsync(ct);
                if (fromLocal.Count > 0)
                {
                    await _repository.PushToCloudAsync(fromLocal, ct);
                    await _repository.MarkSyncedLocalAsync(fromLocal, ct);
                }

                var total = fromCloud.Count + fromLocal.Count;
                if (total > 0)
                {
                    await _repository.WriteSyncLogLocalAsync("SUCCESS", SyncType, total, null, ct);
                    _logger.LogInformation("Sync attendance bidireccional OK, nube->local={Pull}, local->nube={Push}",
                        fromCloud.Count, fromLocal.Count);
                }

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = total,
                    Message = total == 0
                        ? "No hay marcajes pendientes en ninguna dirección."
                        : $"Sincronizados: {fromCloud.Count} de nube->local y {fromLocal.Count} de local->nube."
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
            // Causa real de MySQL (procedimiento faltante, error de datos, o conexión caída).
            var causa = ex.InnerException?.Message ?? ex.Message;

            var esSinConexion = causa.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase);
            var prefijo = esSinConexion
                ? "SIN CONEXION A INTERNET / NUBE NO DISPONIBLE: "
                : "ERROR AL SINCRONIZAR: ";

            var mensaje = prefijo + causa;
            if (mensaje.Length > 255) mensaje = mensaje.Substring(0, 255);

            _logger.LogWarning("Sync attendance -> nube FALLÓ (endpoint). {Mensaje}", causa);

            // 1) Base de errores compartida (SQL Server db_errors_log). No relanza si falla.
            await _errorLog.RegisterAsync(
                $"POST /api/attendancesync/execute [{SyncType}]",
                StatusCodes.Status503ServiceUnavailable,
                causa,
                HttpContext.GetClientIp(),
                "SYNC");

            // 2) sync_log local (best-effort).
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
