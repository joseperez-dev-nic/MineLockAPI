using System;

namespace MineLock.Api.Models
{
    /// <summary>
    /// Coincide con sp_dashboard_active. Ya no trae minutes_inside ni level_code;
    /// si necesitas resaltar por umbral de tiempo en el board, calcula
    /// (DateTime.Now - EntryTime) en el frontend o aquí mismo al mapear.
    /// </summary>
    public class DashboardActiveItem
    {
        public long SessionId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? JobPosition { get; set; }
        public int LevelId { get; set; }
        public string? LevelName { get; set; }
        public DateTime EntryTime { get; set; }
    }
}