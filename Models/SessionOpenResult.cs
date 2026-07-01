using System;

namespace MineLock.Api.Models
{
    /// <summary>
    /// Coincide con el SELECT final de sp_session_open (la sesión recién creada).
    /// LevelCode/LevelName pueden venir null porque el JOIN con level es LEFT JOIN.
    /// </summary>
    public class SessionOpenResult
    {
        public long SessionId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? LevelCode { get; set; }
        public string? LevelName { get; set; }
        public DateTime EntryTime { get; set; }
    }
}
