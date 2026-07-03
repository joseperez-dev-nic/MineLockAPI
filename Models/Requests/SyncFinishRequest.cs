namespace RampaSegura.Api.Models.Requests
{
    public class SyncFinishRequest
    {
        public long SyncId { get; set; }

        // Debe ser exactamente SUCCESS, FAILED o PARTIAL (columna ENUM en sync_log).
        public string Status { get; set; } = string.Empty;

        public int RowsSent { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
