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
    }
}
