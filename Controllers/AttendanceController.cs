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
    public class AttendanceController : ControllerBase
    {
        private readonly AttendanceRepository _repository;

        public AttendanceController(AttendanceRepository repository)
        {
            _repository = repository;
        } 
        
        [HttpPost("entry")]
        public async Task<ActionResult<object>> OpenSession([FromBody] SessionOpenRequest request)
        {
            await _repository.OpenSessionAsync(request.PersonId, request.LevelId, request.EntryTime);
            return Ok(new { status = "OK" });
        }

        [HttpPost("exit")]
        public async Task<ActionResult<object>> CloseSession([FromBody] SessionCloseRequest request)
        {
            await _repository.CloseSessionAsync(request.PersonId, request.ExitTime);
            return Ok(new { status = "OK" });
        }
 
        [HttpGet("dashboard")]
        public async Task<ActionResult<List<DashboardActiveItem>>> GetDashboard()
        {
            var data = await _repository.GetDashboardActiveAsync();
            return Ok(data);
        }
    }
}
