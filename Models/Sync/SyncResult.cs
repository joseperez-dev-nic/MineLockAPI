namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Resultado de un ciclo de sincronización disparado por el endpoint.
    /// </summary>
    public class SyncResult
    {
        public string Status { get; set; } = "SUCCESS"; // SUCCESS | FAILED
        public int RowsSent { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }
    }
}
