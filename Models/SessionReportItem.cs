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
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? JobPosition { get; set; }
        public string? Department { get; set; }
        public string? LevelName { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public TimeSpan? TimeInside { get; set; }
        public string? Status { get; set; }
    }
}
