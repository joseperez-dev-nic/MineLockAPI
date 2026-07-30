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

        /// GET /api/person/photo/{employeeCode}
        [HttpGet("photo/{employeeCode}")]
        public async Task<IActionResult> GetPhoto(string employeeCode)
        {
            var (photoData, mimeType) = await _repository.GetPhotoByCodeAsync(employeeCode);
            if (photoData == null) return NotFound();
            return File(photoData, mimeType ?? "image/jpeg");
        }

        /// POST /api/person/sync-photos
        [HttpPost("sync-photos")]
        public async Task<ActionResult<object>> SyncPhotosFromNcheck()
        {
            var rowsAffected = await _repository.SyncPhotosFromNcheckAsync();
            return Ok(new { status = "OK", rowsAffected });
        }

        /// GET /api/person/photo-sync-interval
        [HttpGet("photo-sync-interval")]
        public async Task<ActionResult<object>> GetPhotoSyncInterval()
        {
            var minutes = await _repository.GetPhotoSyncIntervalAsync();
            return Ok(new { intervalMinutes = minutes });
        }

        /// PUT /api/person/photo-sync-interval
        [HttpPut("photo-sync-interval")]
        public async Task<ActionResult<object>> SetPhotoSyncInterval([FromBody] PhotoSyncIntervalRequest request)
        {
            if (request.IntervalMinutes < 1 || request.IntervalMinutes > 1440)
                return BadRequest(new { error = "El intervalo debe ser entre 1 y 1440 minutos." });

            await _repository.SetPhotoSyncIntervalAsync(request.IntervalMinutes);
            return Ok(new { status = "OK", intervalMinutes = request.IntervalMinutes });
        }
    }

    public class PhotoSyncIntervalRequest
    {
        public int IntervalMinutes { get; set; }
    }
}
