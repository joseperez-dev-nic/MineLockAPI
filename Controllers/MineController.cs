using RampaSegura.Api.Models;
using RampaSegura.Api.Models.Requests;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MineController : ControllerBase
    {
        private readonly MineRepository _repository;

        public MineController(MineRepository repository)
        {
            _repository = repository;
        }

        /// POST /api/mine
        [HttpPost]
        public async Task<ActionResult<object>> Create([FromBody] MineCreateRequest request)
        {
            var mineId = await _repository.InsertAsync(
                request.MineName,
                request.Location,
                request.Country,
                request.TimezoneName,
                request.UtcOffsetMinutes!.Value
            );
            return Ok(new { status = "OK", mineId });
        }

        /// GET /api/mine
        [HttpGet]
        public async Task<ActionResult<List<Mine>>> List([FromQuery] bool onlyActive = false)
        {
            var data = await _repository.ListAsync(onlyActive);
            return Ok(data);
        }

        /// GET /api/mine/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Mine>> GetById(int id)
        {
            var mine = await _repository.GetByIdAsync(id);
            if (mine == null)
            {
                return NotFound(new { error = "MINE_NOT_FOUND" });
            }
            return Ok(mine);
        }

        /// PUT /api/mine
        [HttpPut]
        public async Task<ActionResult<object>> Update([FromBody] MineUpdateRequest request)
        {
            await _repository.UpdateAsync(
                request.MineId!.Value,
                request.MineName,
                request.Location,
                request.Country,
                request.TimezoneName,
                request.UtcOffsetMinutes!.Value
            );
            return Ok(new { status = "OK" });
        }

        /// PUT /api/mine/{id}/deactivate
        [HttpPut("{id}/deactivate")]
        public async Task<ActionResult<object>> Deactivate(int id)
        {
            await _repository.DeactivateAsync(id);
            return Ok(new { status = "OK" });
        }

        /// PUT /api/mine/{id}/activate
        [HttpPut("{id}/activate")]
        public async Task<ActionResult<object>> Activate(int id)
        {
            await _repository.ActivateAsync(id);
            return Ok(new { status = "OK" });
        }
    }
}
