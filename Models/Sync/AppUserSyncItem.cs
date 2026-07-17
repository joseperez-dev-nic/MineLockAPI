using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de app_user (usuarios de la aplicación). No tiene is_synced, así que
    /// se hace upsert de todos los usuarios por user_id en cada llamada.
    /// </summary>
    public class AppUserSyncItem
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? EmployeeCode { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
