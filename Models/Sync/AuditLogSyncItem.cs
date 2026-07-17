using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de audit_log para replicar a la nube. Se sincroniza junto con sync_log
    /// (mismo endpoint). No tiene is_synced, así que se hace upsert de todas las
    /// filas por audit_id.
    /// </summary>
    public class AuditLogSyncItem
    {
        public long AuditId { get; set; }
        public string? ChangeType { get; set; }
        public string? Description { get; set; }
        public long? ChangedByUserId { get; set; }
        public string? ChangedByUsername { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ClientIp { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
