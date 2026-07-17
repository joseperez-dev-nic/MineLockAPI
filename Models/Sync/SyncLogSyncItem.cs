using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de sync_log para replicar a la nube. sync_log no tiene is_synced, así
    /// que se hace upsert de todas las filas (por sync_id) en cada llamada.
    /// Nota: este sync NO escribe a su vez en sync_log (evita el "log del log").
    /// </summary>
    public class SyncLogSyncItem
    {
        public long SyncId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string? Status { get; set; }
        public string? SyncType { get; set; }
        public int? RowsSent { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
