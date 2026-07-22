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
    /// Replica las bitácoras LOCAL -> NUBE (sync_log + audit_log) en una sola llamada.
    /// Envía todas las filas (upsert por sync_id / audit_id). A diferencia de los otros
    /// syncs, este NO escribe en sync_log (evita el "log del log"); los fallos solo van
    /// a la base de errores.
    /// </summary>
    [LocalOnly]
    [ApiController]
    [Route("api/[controller]")]
    public class SyncLogSyncController : ControllerBase
    {
        private const string SyncType = "SYNCLOG";

        private readonly SyncLogSyncRepository _repository;
        private readonly ErrorLogRepository _errorLog;
        private readonly ILogger<SyncLogSyncController> _logger;

        public SyncLogSyncController(
            SyncLogSyncRepository repository,
            ErrorLogRepository errorLog,
            ILogger<SyncLogSyncController> logger)
        {
            _repository = repository;
            _errorLog = errorLog;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/synclogsync/execute
        /// Sincroniza las tres tablas de bitácora en una sola llamada:
        /// 1) Lee sync_log, audit_log y attendance_session_edit_log en local.
        /// 2) Las envía (upsert) a la nube.
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                // sync_log y audit_log: local -> nube (se escriben en local).
                var filas = await _repository.GetSourceLocalAsync(ct);
                var auditorias = await _repository.GetAuditSourceLocalAsync(ct);

                // edit_log: BIDIRECCIONAL (las correcciones pueden hacerse en ambos lados).
                var editLocal = await _repository.GetEditLogLocalAsync(ct);
                var editCloud = await _repository.GetEditLogCloudAsync(ct);

                await _repository.PushToCloudAsync(filas, ct);
                await _repository.PushAuditToCloudAsync(auditorias, ct);
                await _repository.PushEditLogToCloudAsync(editLocal, ct);   // local -> nube
                await _repository.ApplyEditLogToLocalAsync(editCloud, ct);  // nube  -> local

                var total = filas.Count + auditorias.Count + editLocal.Count + editCloud.Count;
                if (total == 0)
                {
                    return Ok(new SyncResult
                    {
                        Status = "SUCCESS",
                        RowsSent = 0,
                        Message = "No hay bitácoras para sincronizar."
                    });
                }

                _logger.LogInformation(
                    "Sync bitácoras OK, sync_log={SyncRows}, audit_log={AuditRows}, edit_log(local->nube={EL}, nube->local={EC})",
                    filas.Count, auditorias.Count, editLocal.Count, editCloud.Count);

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = total,
                    Message = $"sync_log={filas.Count}, audit_log={auditorias.Count}, edit_log(subidas={editLocal.Count}, bajadas={editCloud.Count})."
                });
            }
            catch (DataAccessException ex)
            {
                var causa = ex.InnerException?.Message ?? ex.Message;

                var esSinConexion = causa.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase);
                var prefijo = esSinConexion
                    ? "SIN CONEXION A INTERNET / NUBE NO DISPONIBLE: "
                    : "ERROR AL SINCRONIZAR: ";

                var mensaje = prefijo + causa;
                if (mensaje.Length > 255) mensaje = mensaje.Substring(0, 255);

                _logger.LogWarning("Sync sync_log -> nube FALLÓ (endpoint). {Mensaje}", causa);

                // Solo base de errores (este endpoint no escribe en sync_log).
                await _errorLog.RegisterAsync(
                    $"POST /api/synclogsync/execute [{SyncType}]",
                    StatusCodes.Status503ServiceUnavailable,
                    causa,
                    HttpContext.GetClientIp(),
                    "SYNC");

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new SyncResult
                {
                    Status = "FAILED",
                    RowsSent = 0,
                    ErrorMessage = mensaje
                });
            }
        }
    }
}
