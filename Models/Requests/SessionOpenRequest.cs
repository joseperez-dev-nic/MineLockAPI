using System.ComponentModel.DataAnnotations;

namespace RampaSegura.Api.Models.Requests
{
    public class SessionOpenRequest
    {
        [Required(ErrorMessage = "PERSON_ID_REQUIRED")]
        public long? PersonId { get; set; }

        [Required(ErrorMessage = "LEVEL_ID_REQUIRED")]
        public int? LevelId { get; set; }

        [Required(ErrorMessage = "ENTRY_TIME_REQUIRED")]
        public long? EntryTime { get; set; }

        [Required(ErrorMessage = "UTC_OFFSET_REQUIRED")]
        public long? UtcOffsetSeconds { get; set; }
    }
}