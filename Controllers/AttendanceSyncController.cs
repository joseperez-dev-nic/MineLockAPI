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
    /// Sincronización de attendance_session LOCAL -> NUBE, disparada bajo demanda.
    /// Se debe llamar desde el despliegue LOCAL de la API (el que tiene acceso a la
    /// base local). El despliegue en la nube simplemente no llama este endpoint.
    /// </summary>
    [Authorize(Roles = RoleCodes.Admin)]
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
        /// Ejecuta un ciclo completo:
        ///   1) Lee marcajes pendientes (is_synced = 0) de la base local.
        ///   2) Los envía (upsert) a la nube.
        ///   3) Marca is_synced = 1 en local solo de lo enviado.
        ///   4) Registra el resultado en sync_log (local).
        /// Cualquier fallo se registra en la base de errores compartida y en sync_log,
        /// y se responde 503 con el detalle real.
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                var pending = await _repository.GetPendingLocalAsync(ct);

                if (pending.Count == 0)
                {
                    return Ok(new SyncResult
                    {
                        Status = "SUCCESS",
                        RowsSent = 0,
                        Message = "No hay marcajes pendientes de sincronizar."
                    });
                }

                await _repository.PushToCloudAsync(pending, ct);
                await _repository.MarkSyncedLocalAsync(pending, ct);
                await _repository.WriteSyncLogLocalAsync("SUCCESS", SyncType, pending.Count, null, ct);

                _logger.LogInformation("Sync attendance -> nube OK (endpoint), filas={Rows}", pending.Count);

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = pending.Count,
                    Message = $"{pending.Count} marcaje(s) sincronizado(s) a la nube."
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
