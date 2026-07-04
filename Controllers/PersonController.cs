using RampaSegura.Api.Models;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonController : ControllerBase
    {
        private readonly PersonRepository _repository;

        public PersonController(PersonRepository repository)
        {
            _repository = repository;
        }

        /// GET /api/person
        [HttpGet]
        public async Task<ActionResult<List<Person>>> GetPersonList()
        {
            var data = await _repository.GetPersonListAsync();
            return Ok(data);
        }
    }
}
