using RampaSegura.Api.Common;
using RampaSegura.Api.Models;
using RampaSegura.Api.Models.Requests;
using RampaSegura.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RampaSegura.Api.Controllers
{
    [Authorize(Roles = RoleCodes.Admin)]
    [ApiController]
    [Route("api/[controller]")]
    public class AlertSettingsController : ControllerBase
    {
        private readonly AlertSettingRepository _repository;

        public AlertSettingsController(AlertSettingRepository repository)
        {
            _repository = repository;
        }

        /// GET /api/alertsettings -- umbrales actuales de advertencia y alerta.
        [HttpGet]
        public async Task<ActionResult<AlertSetting>> Get()
        {
            var settings = await _repository.GetAsync();
            if (settings is null)
            {
                return NotFound(new { error = "ALERT_SETTINGS_NOT_FOUND" });
            }
            return Ok(settings);
        }

        /// PUT /api/alertsettings -- actualiza los umbrales y audita el cambio.
        [HttpPut]
        public async Task<ActionResult<AlertSetting>> Update([FromBody] AlertSettingUpdateRequest request)
        {
            var updated = await _repository.UpdateAsync(
                request.WarnLimitHours!.Value,
                request.TurnLimitHours!.Value,
                request.UserId!.Value,
                HttpContext.GetClientIp()
            );
            return Ok(updated);
        }

        /// GET /api/alertsettings/audit -- bitácora de cambios de parámetros.
        [HttpGet("audit")]
        public async Task<ActionResult<List<AuditLogItem>>> Audit(
            [FromQuery] string? changeType = "ALERT_THRESHOLDS_UPDATE",
            [FromQuery] int limit = 100)
        {
            var log = await _repository.GetAuditLogAsync(changeType, limit);
            return Ok(log);
        }
    }
}
