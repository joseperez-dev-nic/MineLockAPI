using System;
using System.ComponentModel.DataAnnotations;

namespace RampaSegura.Api.Models.Requests
{
    /// <summary>
    /// Corrección de la hora de salida de una sesión YA cerrada, hecha por un
    /// administrador. Cada corrección queda auditada en attendance_session_edit_log.
    /// Igual que el cierre manual, la hora es LOCAL (de la mina) y no lleva offset:
    /// sp_session_close_correct usa el time_zone de la sesión.
    /// </summary>
    public class SessionCloseCorrectRequest
    {
        [Required(ErrorMessage = "SESSION_ID_REQUIRED")]
        public long? SessionId { get; set; }

        [Required(ErrorMessage = "EXIT_TIME_REQUIRED")]
        public DateTime? NewExitTimeLocal { get; set; }

        [Required(ErrorMessage = "USER_ID_REQUIRED")]
        public long? UserId { get; set; }

        [Required(ErrorMessage = "REASON_REQUIRED")]
        [StringLength(255, ErrorMessage = "REASON_TOO_LONG")]
        public string? Reason { get; set; }
    }
}
