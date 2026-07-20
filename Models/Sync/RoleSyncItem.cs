using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de la tabla role. Se sincroniza junto con app_user (mismo endpoint) y
    /// SIEMPRE antes que los usuarios: app_user.role_id referencia role(role_id).
    /// Sin is_synced, así que se hace upsert de todas las filas por role_id.
    /// </summary>
    public class RoleSyncItem
    {
        public int RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
