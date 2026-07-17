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
        /// Sincroniza las dos tablas de bitácora en una sola llamada:
        /// 1) Lee todas las filas de sync_log y de audit_log en local.
        /// 2) Las envía (upsert) a la nube.
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<SyncResult>> Execute(CancellationToken ct)
        {
            try
            {
                var filas = await _repository.GetSourceLocalAsync(ct);
                var auditorias = await _repository.GetAuditSourceLocalAsync(ct);

                if (filas.Count == 0 && auditorias.Count == 0)
                {
                    return Ok(new SyncResult
                    {
                        Status = "SUCCESS",
                        RowsSent = 0,
                        Message = "No hay registros de sync_log ni audit_log para sincronizar."
                    });
                }

                await _repository.PushToCloudAsync(filas, ct);
                await _repository.PushAuditToCloudAsync(auditorias, ct);

                var total = filas.Count + auditorias.Count;
                _logger.LogInformation(
                    "Sync bitácoras -> nube OK (endpoint), sync_log={SyncRows}, audit_log={AuditRows}",
                    filas.Count, auditorias.Count);

                return Ok(new SyncResult
                {
                    Status = "SUCCESS",
                    RowsSent = total,
                    Message = $"{filas.Count} registro(s) de sync_log y {auditorias.Count} de audit_log sincronizado(s) a la nube."
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
