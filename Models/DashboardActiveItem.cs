using System;

namespace MineLock.Api.Models
{
    /// <summary>
    /// Coincide con sp_dashboard_active. Fuente del board por nivel en laboratrack.html.
    /// minutes_inside ya viene calculado por MySQL (TIMESTAMPDIFF); si necesitas
    /// resaltar en rojo por umbral, ese cálculo lo haces en el frontend comparando
    /// MinutesInside contra el límite que definan.
    /// </summary>
    public class DashboardActiveItem
    {
        public long SessionId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? LevelCode { get; set; }
        public string? LevelName { get; set; }
        public DateTime EntryTime { get; set; }
        public int MinutesInside { get; set; }
    }
}
