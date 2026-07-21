using System;
using System.ComponentModel.DataAnnotations;

namespace RampaSegura.Api.Models.Requests
{
    /// <summary>
    /// Cierre MANUAL de una sesión abierta, hecho por un administrador.
    /// A diferencia del cierre normal (que manda un timestamp Unix + offset),
    /// aquí la hora de salida es una fecha-hora LOCAL escrita a mano por el admin,
    /// así que se envía tal cual (sp_session_close_manual la toma como está).
    /// </summary>
    public class SessionCloseManualRequest
    {
        [Required(ErrorMessage = "PERSON_ID_REQUIRED")]
        public long? PersonId { get; set; }

        [Required(ErrorMessage = "EXIT_TIME_REQUIRED")]
        public DateTime? ExitTimeLocal { get; set; }

        [Required(ErrorMessage = "USER_ID_REQUIRED")]
        public long? UserId { get; set; }

        [Required(ErrorMessage = "REASON_REQUIRED")]
        [StringLength(255, ErrorMessage = "REASON_TOO_LONG")]
        public string? Reason { get; set; }
    }
}
