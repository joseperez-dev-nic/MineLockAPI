using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_sync_pending: sesiones tocadas desde el último sync
    /// exitoso (o todas, si nunca ha corrido un ciclo de sync).
    /// </summary>
    public class SyncPendingItem
    {
        public long SessionId { get; set; }
        public long PersonId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public int? LevelId { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
