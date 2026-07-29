using RampaSegura.Api.Models.Sync;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    /// <summary>
    /// Historial de sincronizaciones (sync_log) contra la base OPERATIVA.
    /// Disponible en los dos despliegues: en Local muestra el sync_log local,
    /// en la Nube muestra el sync_log ya replicado ahí (sync_log es local→nube).
    /// </summary>
    [ApiController]
    [Route("api/syncstatus")]
    public class SyncHistoryController : ControllerBase
    {
        private const int LimitePorDefecto = 50;
        private const int LimiteMaximo = 500;

        private readonly SyncHistoryRepository _repository;

        public SyncHistoryController(SyncHistoryRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// GET /api/syncstatus/history?syncType=ATTENDANCE&amp;fechaDesde=2026-07-01&amp;fechaHasta=2026-07-20&amp;limit=50
        /// Historial de sincronizaciones (sync_log), del más reciente al más viejo.
        /// Todos los filtros son opcionales.
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<List<SyncLogSyncItem>>> GetHistory(
            [FromQuery] string? syncType,
            [FromQuery] DateOnly? fechaDesde,
            [FromQuery] DateOnly? fechaHasta,
            [FromQuery] int? limit,
            CancellationToken ct)
        {
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value < fechaDesde.Value)
            {
                return BadRequest(new { error = "RANGO_FECHAS_INVALIDO" });
            }

            var tope = Math.Clamp(limit ?? LimitePorDefecto, 1, LimiteMaximo);

            var data = await _repository.GetHistoryAsync(syncType, fechaDesde, fechaHasta, tope, ct);
            return Ok(data);
        }
    }
}
