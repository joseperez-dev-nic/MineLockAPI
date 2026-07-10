namespace RampaSegura.Api.Models
{
    public class Mine
    {
        public int MineId { get; set; }
        public string MineName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string TimezoneName { get; set; } = string.Empty;
        public int UtcOffsetMinutes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
