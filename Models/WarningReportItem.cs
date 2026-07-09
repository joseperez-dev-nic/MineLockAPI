using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_warning_report: sesiones (abiertas o cerradas) que superaron
    /// las 8 horas (480 min) dentro de la mina.
    /// </summary>
    public class WarningReportItem
    {
        public long SessionId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? JobPosition { get; set; }
        public string? Department { get; set; }
        public string? LevelName { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public int MinutosDentro { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
