using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_session_edit_history: una entrada del historial de ediciones
    /// de una sesión (attendance_session_edit_log). edited_by es el username, no el id.
    /// </summary>
    public class SessionEditLogItem
    {
        public long EditId { get; set; }
        public long SessionId { get; set; }
        public DateTime EditedAt { get; set; }
        public string? EditedBy { get; set; }
        public string FieldChanged { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Reason { get; set; }
    }
}
