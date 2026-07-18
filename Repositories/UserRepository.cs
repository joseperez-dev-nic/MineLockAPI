using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Models;
using MySqlConnector;
using System;
using System.Data;
using System.Threading.Tasks;

namespace RampaSegura.Api.Repositories
{
    public class UserRepository
    {
        private readonly IRampaSeguraConnectionFactory _factory;

        public UserRepository(IRampaSeguraConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_user_get_by_username(p_login) -- p_login puede ser username o email.
        /// Devuelve null si no hay match; la verificación de password e is_active
        /// se hace en el controller, aquí solo se trae el registro.
        /// </summary>
        public async Task<UserAccount?> GetByUsernameOrEmailAsync(string login)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_user_get_by_username", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_login", login);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new UserAccount
                {
                    UserId = reader.GetInt64("user_id"),
                    Username = reader.GetString("username"),
                    EmployeeCode = reader.IsDBNull(reader.GetOrdinal("employee_code")) ? null : reader.GetString("employee_code"),
                    PasswordHash = reader.GetString("password_hash"),
                    FullName = reader.GetString("full_name"),
                    Email = reader.GetString("email"),
                    IsActive = reader.GetBoolean("is_active"),
                    RoleCode = reader.IsDBNull(reader.GetOrdinal("role_code"))
                        ? RoleCodes.Viewer
                        : reader.GetString("role_code")
                };
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, ex.Message, ex);
            }
            catch (Exception ex) when (ex is not DataAccessException)
            {
                throw new DataAccessException("Error inesperado al buscar el usuario", ex);
            }
        }

        /// <summary>
        /// sp_user_touch_last_login(p_user_id) -- se llama después de un login exitoso.
        /// </summary>
        public async Task TouchLastLoginAsync(long userId)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_user_touch_last_login", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_user_id", userId);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, ex.Message, ex);
            }
        }
    }
}
