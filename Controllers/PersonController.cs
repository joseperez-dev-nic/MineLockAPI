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

        /// GET /api/person/list
        [HttpGet("list")]
        public async Task<ActionResult<List<Person>>> GetLBPersonList()
        {
            var data = await _repository.GetLBPersonListAsync();
            return Ok(data);
        }

        /// POST /api/person/sync
        [HttpPost("sync")]
        public async Task<ActionResult<object>> SyncFromNcheck()
        {
            var rowsAffected = await _repository.SyncAllFromNcheckAsync();
            return Ok(new { status = "OK", rowsAffected });
        }

        /// GET /api/person/photos
        /// Todos los empleados activos con foto (employee_code + base64).
        /// Lo consume el script de arranque del frontend para guardar las
        /// imágenes en assets/profile-photos/{employee_code}.{ext}.
        [HttpGet("photos")]
        public async Task<ActionResult<List<ProfilePhotoExport>>> GetProfilePhotos()
        {
            var data = await _repository.GetAllProfilePhotosAsync();
            return Ok(data);
        }
    }
}
