using System;

namespace MineLock.Api.Models.Requests
{
    public class SessionCloseRequest
    {
        public string EmployeeCode { get; set; } = string.Empty;

        // Opcional: si no la mandas, la API usa la hora del servidor.
        public DateTime? ExitTime { get; set; }
    }
}
