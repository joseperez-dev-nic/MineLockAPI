using System;

namespace RampaSegura.Api.Models.Sync
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

        /// <summary>Offset en segundos de la entrada. Se sincroniza: la nube lo necesita
        /// para recalcular la columna generada entry_time_utc.</summary>
        public long? TimeZone { get; set; }

        /// <summary>Offset en segundos de la salida. Se sincroniza: la nube lo necesita
        /// para recalcular la columna generada exit_time_utc.</summary>
        public long? ExitTimeZone { get; set; }

        /// <summary>Columna GENERADA en la base. Se lee pero NO se envía a la nube
        /// (MySQL prohíbe escribir columnas generadas); allá se recalcula sola.</summary>
        public TimeSpan? TimeInside { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
