using RampaSegura.Api.Models;
using RampaSegura.Api.Models.Requests;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private static readonly string[] ValidStatuses = { "SUCCESS", "FAILED", "PARTIAL" };

        private readonly SyncRepository _repository;

        public SyncController(SyncRepository repository)
        {
            _repository = repository;
        }

        /// GET /api/sync/pendientes
        [HttpGet("pendientes")]
        public async Task<ActionResult<List<SyncPendingItem>>> GetPending()
        {
            var pending = await _repository.GetPendingAsync();
            return Ok(pending);
        }

        /// POST /api/sync/iniciar
        [HttpPost("iniciar")]
        public async Task<ActionResult<object>> Start()
        {
            var syncId = await _repository.StartSyncAsync();
            return Ok(new { syncId });
        }

        /// POST /api/sync/finalizar
        /// Body: { "syncId": 1, "status": "SUCCESS", "rowsSent": 12, "errorMessage": null }
        [HttpPost("finalizar")]
        public async Task<IActionResult> Finish([FromBody] SyncFinishRequest request)
        {
            var status = request.Status?.ToUpperInvariant() ?? "";
            if (Array.IndexOf(ValidStatuses, status) < 0)
            {
                return BadRequest(new { error = "status debe ser SUCCESS, FAILED o PARTIAL" });
            }

            await _repository.FinishSyncAsync(request.SyncId, status, request.RowsSent, request.ErrorMessage);
            return NoContent();
        }
    }
}
