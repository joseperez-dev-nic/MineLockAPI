using RampaSegura.Api.Models;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LevelsController : ControllerBase
    {
        private readonly LevelRepository _repository;

        public LevelsController(LevelRepository repository)
        {
            _repository = repository;
        }

        /// GET /api/levels
        [HttpGet]
        public async Task<ActionResult<List<Level>>> GetAll()
        {
            var levels = await _repository.GetAllAsync();
            return Ok(levels);
        }
    }
}
