using RampaSegura.Api.Common;
using RampaSegura.Api.Models;
using RampaSegura.Api.Models.Requests;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Mvc; 
using System.ComponentModel.DataAnnotations; 

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
            var entryTime = UnixTimestampConverter.UnixTimestampAFecha(request.EntryTime!.Value);
            await _repository.OpenSessionAsync(request.PersonId!.Value, request.LevelId!.Value, entryTime);
            return Ok(new { status = "OK" });
        }

        [HttpPost("exit")]
        public async Task<ActionResult<object>> CloseSession([FromBody] SessionCloseRequest request)
        {
            var exitTime = UnixTimestampConverter.UnixTimestampAFecha(request.ExitTime!.Value);
            await _repository.CloseSessionAsync(request.PersonId!.Value, exitTime);
            return Ok(new { status = "OK" });
        }
 
        [HttpGet("dashboard")]
        public async Task<ActionResult<List<DashboardActiveItem>>> GetDashboard()
        {
            var data = await _repository.GetDashboardActiveAsync();
            return Ok(data);
        }

        /// GET /api/attendance/report?fechaDesde=2026-07-01&fechaHasta=2026-07-03
        [HttpGet("report")]
        public async Task<ActionResult<List<SessionReportItem>>> GetReport(
            [FromQuery, Required(ErrorMessage = "FECHA_DESDE_REQUIRED")] DateOnly? fechaDesde,
            [FromQuery, Required(ErrorMessage = "FECHA_HASTA_REQUIRED")] DateOnly? fechaHasta)
        {
            if (fechaHasta!.Value < fechaDesde!.Value)
            {
                return BadRequest(new { error = "RANGO_FECHAS_INVALIDO" });
            }

            var data = await _repository.GetReportAsync(fechaDesde.Value, fechaHasta.Value);
            return Ok(data);
        }

        /// GET /api/attendance/warnings?fechaDesde=2026-07-01&fechaHasta=2026-07-03
        /// Ambas fechas son opcionales: si se omiten, trae advertencias de todo el histórico.
        [HttpGet("warnings")]
        public async Task<ActionResult<List<WarningReportItem>>> GetWarnings(
            [FromQuery] DateOnly? fechaDesde,
            [FromQuery] DateOnly? fechaHasta)
        {
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value < fechaDesde.Value)
            {
                return BadRequest(new { error = "RANGO_FECHAS_INVALIDO" });
            }

            var data = await _repository.GetWarningReportAsync(fechaDesde, fechaHasta);
            return Ok(data);
        }
    }
}
