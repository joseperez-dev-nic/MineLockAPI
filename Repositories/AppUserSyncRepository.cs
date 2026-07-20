using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Models.Sync;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace RampaSegura.Api.Repositories
{
    /// <summary>
    /// Sincronización de role + app_user: LOCAL -> NUBE (mismo endpoint).
    /// Sin is_synced: se hace upsert de todas las filas en cada llamada.
    /// ORDEN OBLIGATORIO: primero los roles, después los usuarios, porque
    /// app_user.role_id referencia role(role_id).
    /// OJO: el login corre contra la NUBE, así que allá last_login_at es más fresco que
    /// en local. Por eso sp_appuser_sync_upsert NO pisa last_login_at al actualizar.
    /// </summary>
    public class AppUserSyncRepository
    {
        private readonly IRampaSeguraLocalConnectionFactory _local;
        private readonly IRampaSeguraCloudConnectionFactory _cloud;

        public AppUserSyncRepository(
            IRampaSeguraLocalConnectionFactory local,
            IRampaSeguraCloudConnectionFactory cloud)
        {
            _local = local;
            _cloud = cloud;
        }

        /// <summary>
        /// sp_role_sync_source() -- todos los roles en local.
        /// </summary>
        public async Task<List<RoleSyncItem>> GetRoleSourceLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_role_sync_source", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<RoleSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new RoleSyncItem
                    {
                        // role_id es TINYINT UNSIGNED: Convert evita depender de
                        // cómo lo mapee el driver (byte/sbyte).
                        RoleId = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("role_id"))),
                        RoleCode = reader.GetString("role_code"),
                        RoleName = reader.GetString("role_name"),
                        IsActive = reader.GetBoolean("is_active"),
                        CreatedAt = reader.GetDateTime("created_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al leer los roles en la base local", ex);
            }
        }

        /// <summary>
        /// Upsert de cada rol en la NUBE (sp_role_sync_upsert), en transacción.
        /// Debe correr ANTES que PushToCloudAsync (FK app_user.role_id -> role).
        /// </summary>
        public async Task PushRolesToCloudAsync(IReadOnlyList<RoleSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_role_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_role_id", item.RoleId);
                    cmd.Parameters.AddWithValue("p_role_code", item.RoleCode);
                    cmd.Parameters.AddWithValue("p_role_name", item.RoleName);
                    cmd.Parameters.AddWithValue("p_is_active", item.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("p_created_at", item.CreatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar los roles a la nube", ex);
            }
        }

        /// <summary>
        /// sp_appuser_sync_source() -- todos los usuarios en local.
        /// </summary>
        public async Task<List<AppUserSyncItem>> GetSourceLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_appuser_sync_source", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<AppUserSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new AppUserSyncItem
                    {
                        UserId = reader.GetInt64("user_id"),
                        Username = reader.GetString("username"),
                        EmployeeCode = reader.IsDBNull(reader.GetOrdinal("employee_code")) ? null : reader.GetString("employee_code"),
                        RoleId = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("role_id"))),
                        PasswordHash = reader.GetString("password_hash"),
                        FullName = reader.IsDBNull(reader.GetOrdinal("full_name")) ? null : reader.GetString("full_name"),
                        Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email"),
                        IsActive = reader.GetBoolean("is_active"),
                        LastLoginAt = reader.IsDBNull(reader.GetOrdinal("last_login_at")) ? null : reader.GetDateTime("last_login_at"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al leer los usuarios en la base local", ex);
            }
        }

        /// <summary>
        /// Upsert de cada usuario en la NUBE (sp_appuser_sync_upsert), en transacción.
        /// </summary>
        public async Task PushToCloudAsync(IReadOnlyList<AppUserSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_appuser_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_user_id", item.UserId);
                    cmd.Parameters.AddWithValue("p_username", item.Username);
                    cmd.Parameters.AddWithValue("p_employee_code", (object?)item.EmployeeCode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_role_id", item.RoleId);
                    cmd.Parameters.AddWithValue("p_password_hash", item.PasswordHash);
                    cmd.Parameters.AddWithValue("p_full_name", (object?)item.FullName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_email", (object?)item.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_is_active", item.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("p_last_login_at", (object?)item.LastLoginAt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_created_at", item.CreatedAt);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar los usuarios a la nube", ex);
            }
        }

        /// <summary>
        /// sp_sync_log_write(p_status, p_sync_type, p_rows_sent, p_error) en la base LOCAL.
        /// </summary>
        public async Task WriteSyncLogLocalAsync(string status, string syncType, int rowsSent, string? errorMessage, CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_sync_log_write", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("p_status", status.ToUpperInvariant());
                cmd.Parameters.AddWithValue("p_sync_type", syncType.ToUpperInvariant());
                cmd.Parameters.AddWithValue("p_rows_sent", rowsSent);
                cmd.Parameters.AddWithValue("p_error", (object?)errorMessage ?? DBNull.Value);

                await cnn.OpenAsync(ct);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al registrar en sync_log", ex);
            }
        }
    }
}
