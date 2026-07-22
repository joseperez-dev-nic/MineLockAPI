using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de attendance_session_edit_log (historial de correcciones de salida).
    /// Se sincroniza junto con sync_log y audit_log (mismo endpoint), local -> nube,
    /// upsert por edit_id.
    /// </summary>
    public class AttendanceEditLogSyncItem
    {
        public long EditId { get; set; }
        public long SessionId { get; set; }
        public long EditedByUserId { get; set; }
        public DateTime EditedAt { get; set; }
        public string FieldChanged { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Reason { get; set; }

        /// <summary>Borrado lógico: 1 = eliminado (no se muestra), pero la fila viaja
        /// por el sync para que el borrado se refleje en ambas bases.</summary>
        public bool IsDeleted { get; set; }

        /// <summary>Quién marcó el borrado (app_user.user_id).</summary>
        public long? DeletedByUserId { get; set; }

        /// <summary>Cuándo se marcó el borrado (UTC).</summary>
        public DateTime? DeletedAt { get; set; }
    }
}
