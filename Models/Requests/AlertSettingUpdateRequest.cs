using System.ComponentModel.DataAnnotations;

namespace RampaSegura.Api.Models.Requests
{
    public class AlertSettingUpdateRequest
    {
        [Required(ErrorMessage = "WARN_LIMIT_HOURS_REQUIRED")]
        [Range(0.01, 24, ErrorMessage = "WARN_LIMIT_HOURS_OUT_OF_RANGE")]
        public decimal? WarnLimitHours { get; set; }

        [Required(ErrorMessage = "TURN_LIMIT_HOURS_REQUIRED")]
        [Range(0.01, 24, ErrorMessage = "TURN_LIMIT_HOURS_OUT_OF_RANGE")]
        public decimal? TurnLimitHours { get; set; }

        // Quién realiza el cambio. La API se autentica por X-Api-Key (sin identidad
        // de usuario), así que el frontend envía el id del usuario logueado para
        // poder auditar la modificación.
        [Required(ErrorMessage = "USER_ID_REQUIRED")]
        public long? UserId { get; set; }
    }
}
