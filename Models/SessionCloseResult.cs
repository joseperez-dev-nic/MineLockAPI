using System;

namespace MineLock.Api.Models
{
    /// <summary>
    /// Coincide con el SELECT final de sp_session_close (la sesión recién cerrada).
    /// </summary>
    public class SessionCloseResult
    {
        public long SessionId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
    }
}
