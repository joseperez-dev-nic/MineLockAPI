namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_user_get_by_username. Incluye password_hash e is_active
    /// a propósito: los necesita el repository/controller para validar el login,
    /// pero el controller nunca debe serializar este objeto tal cual hacia el cliente.
    /// </summary>
    public class UserAccount
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? EmployeeCode { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        /// <summary>
        /// Código del rol (ADMIN / VIEWER). Es lo que se firma en el JWT y lo que
        /// evalúan los [Authorize(Roles = ...)] de los controladores.
        /// </summary>
        public string RoleCode { get; set; } = RoleCodes.Viewer;
    }

    /// <summary>
    /// Códigos de rol. Deben calzar exactamente con role.role_code en la base.
    /// Se usan como constantes para no repetir literales en los [Authorize].
    /// </summary>
    public static class RoleCodes
    {
        public const string Admin = "ADMIN";
        public const string Viewer = "VIEWER";
    }
}
