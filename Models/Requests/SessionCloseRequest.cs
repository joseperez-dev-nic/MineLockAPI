using System.ComponentModel.DataAnnotations;

namespace RampaSegura.Api.Models.Requests
{
    public class SessionCloseRequest
    {
        [Required(ErrorMessage = "PERSON_ID_REQUIRED")]
        public long? PersonId { get; set; }

        [Required(ErrorMessage = "EXIT_TIME_REQUIRED")]
        public long? ExitTime { get; set; }

        [Required(ErrorMessage = "UTC_OFFSET_REQUIRED")]
        public long? UtcOffsetSeconds { get; set; }
    }
}

