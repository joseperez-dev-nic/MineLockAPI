using System;

namespace MineLock.Api.Models.Requests
{
    public class SessionOpenRequest
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string LevelCode { get; set; } = string.Empty;

        // Opcional: si no la mandas, la API usa la hora del servidor.
        public DateTime? EntryTime { get; set; }
    }
}
