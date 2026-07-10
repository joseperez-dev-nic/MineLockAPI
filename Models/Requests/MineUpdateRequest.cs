using System.ComponentModel.DataAnnotations;

namespace RampaSegura.Api.Models.Requests
{
    public class MineUpdateRequest
    {
        [Required(ErrorMessage = "MINE_ID_REQUIRED")]
        public int? MineId { get; set; }

        [Required(ErrorMessage = "MINE_NAME_REQUIRED")]
        public string MineName { get; set; } = string.Empty;

        [Required(ErrorMessage = "LOCATION_REQUIRED")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "COUNTRY_REQUIRED")]
        public string Country { get; set; } = string.Empty;

        [Required(ErrorMessage = "TIMEZONE_NAME_REQUIRED")]
        public string TimezoneName { get; set; } = string.Empty;

        [Required(ErrorMessage = "UTC_OFFSET_MINUTES_REQUIRED")]
        public int? UtcOffsetMinutes { get; set; }
    }
}
