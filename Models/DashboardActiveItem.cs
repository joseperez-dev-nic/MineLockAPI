using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_dashboard_active. minutes_inside y tiempo_dentro se calculan
    /// contra NOW() en el momento de la consulta (no se guardan), así que cambian
    /// en cada llamada mientras la sesión siga abierta.
    /// </summary>
    public class DashboardActiveItem
    {
        public long SessionId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? JobPosition { get; set; }
        public int LevelId { get; set; }
        public string? LevelName { get; set; }
        public DateTime EntryTime { get; set; }
        public int MinutesInside { get; set; }
        public string TiempoDentro { get; set; } = string.Empty;
    }
}