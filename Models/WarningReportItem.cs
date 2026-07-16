using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_warning_report: sesiones cerradas que superaron el límite de
    /// advertencia. Los límites salen de alert_threshold_setting (warn_limit_hours /
    /// turn_limit_hours), no están fijos en el SP.
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

        /// <summary>
        /// "Turno excedido" si superó turn_limit_hours, "Advertencia" si superó
        /// warn_limit_hours. Lo calcula el SP contra alert_threshold_setting.
        /// </summary>
        public string? NivelAlerta { get; set; }
    }
}
