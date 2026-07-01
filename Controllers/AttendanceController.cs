using MineLock.Api.Models;
using MineLock.Api.Models.Requests;
using MineLock.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MineLock.Api.Controllers
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

        /// POST /api/attendance/entrada
        /// Body: { "employeeCode": "COL-0427", "levelCode": "N01", "entryTime": null }
        [HttpPost("entrada")]
        public async Task<ActionResult<SessionOpenResult>> OpenSession([FromBody] SessionOpenRequest request)
        {
            var session = await _repository.OpenSessionAsync(request.EmployeeCode, request.LevelCode, request.EntryTime);
            return Ok(session);
        }

        /// POST /api/attendance/salida
        /// Body: { "employeeCode": "COL-0427", "exitTime": null }
        [HttpPost("salida")]
        public async Task<ActionResult<SessionCloseResult>> CloseSession([FromBody] SessionCloseRequest request)
        {
            var session = await _repository.CloseSessionAsync(request.EmployeeCode, request.ExitTime);
            return Ok(session);
        }

        /// GET /api/attendance/dashboard
        [HttpGet("dashboard")]
        public async Task<ActionResult<List<DashboardActiveItem>>> GetDashboard()
        {
            var data = await _repository.GetDashboardActiveAsync();
            return Ok(data);
        }
    }
}
