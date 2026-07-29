using RampaSegura.Api.Common;
using RampaSegura.Api.Models.Sync;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    /// <summary>
    /// Consultas de monitoreo de la sincronización (solo lectura, no sincroniza nada).
    /// Se llama en el despliegue LOCAL, que es el que ve las dos bases.
    /// </summary>
    [LocalOnly]
    [ApiController]
    [Route("api/[controller]")]
    public class SyncStatusController : ControllerBase
    {
        private readonly SyncStatusRepository _repository;
        private readonly ILogger<SyncStatusController> _logger;

        public SyncStatusController(
            SyncStatusRepository repository,
            ILogger<SyncStatusController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/syncstatus/attendance
        /// Qué tan al día están los marcajes: última actualización, total y pendientes
        /// en local, contra los mismos datos en la nube.
        /// Si la nube no responde, igual devuelve lo local con el motivo en nubeError
        /// (así el endpoint sirve incluso sin internet).
        /// </summary>
        [HttpGet("attendance")]
        public async Task<ActionResult<AttendanceSyncStatus>> GetAttendanceStatus(CancellationToken ct)
        {
            var resultado = new AttendanceSyncStatus
            {
                Local = await _repository.GetAttendanceStatusLocalAsync(ct)
            };

            // La nube es best-effort: si no hay conexión no se cae el endpoint.
            try
            {
                resultado.Nube = await _repository.GetAttendanceStatusCloudAsync(ct);
            }
            catch (DataAccessException ex)
            {
                var causa = ex.InnerException?.Message ?? ex.Message;
                resultado.NubeError = causa.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase)
                    ? "SIN CONEXION A INTERNET / NUBE NO DISPONIBLE"
                    : causa;

                _logger.LogWarning("No se pudo consultar el estado en la nube. {Mensaje}", causa);
            }

            return Ok(resultado);
        }
    }
}
