using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_session_report. time_inside se asume columna TIME
    /// (TIMEDIFF entre entry_time y exit_time); ajustar si en realidad es
    /// un entero de minutos.
    /// </summary>
    public class SessionReportItem
    {
        public long SessionId { get; set; }
        public long PersonId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? JobPosition { get; set; }
        public string? Department { get; set; }
        public string? LevelName { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public TimeSpan? TimeInside { get; set; }
        public string? Status { get; set; }

        /// <summary>true si la sesión se cerró manualmente (sp_session_close_manual).</summary>
        public bool ClosedManually { get; set; }

        /// <summary>Usuario que cerró la sesión manualmente (app_user.user_id).</summary>
        public long? ClosedByUserId { get; set; }

        /// <summary>Nombre del usuario que cerró manualmente (app_user.full_name).</summary>
        public string? ClosedByName { get; set; }

        /// <summary>Motivo del cierre manual.</summary>
        public string? ClosedReason { get; set; }
    }
}
