using System.ComponentModel.DataAnnotations;

namespace RampaSegura.Api.Models.Requests
{
    public class LoginRequest
    {
        /// <summary>
        /// Puede ser el username o el email; sp_user_get_by_username busca por ambos.
        /// </summary>
        [Required(ErrorMessage = "LOGIN_REQUIRED")]
        public string? Login { get; set; }

        [Required(ErrorMessage = "PASSWORD_REQUIRED")]
        public string? Password { get; set; }
    }
}
