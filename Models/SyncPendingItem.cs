using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Fila de attendance_session pendiente de sincronizar (is_synced = 0).
    /// Incluye TODAS las columnas de la tabla porque la nube es un espejo
    /// exacto y se envía la fila completa.
    /// </summary>
    public class SyncPendingItem
    {
        public long SessionId { get; set; }
        public long PersonId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? JobPosition { get; set; }
        public string? Department { get; set; }
        public int? LevelId { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public TimeSpan? TimeInside { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
