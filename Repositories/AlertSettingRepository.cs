using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RampaSegura.Api.Repositories
{
    public class AlertSettingRepository
    {
        private readonly IRampaSeguraConnectionFactory _factory;

        public AlertSettingRepository(IRampaSeguraConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_alert_settings_get() -- umbrales globales actuales.
        /// </summary>
        public async Task<AlertSetting?> GetAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_alert_settings_get", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return null;
                }
                return MapSetting(reader);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al obtener los parámetros de alerta", ex);
            }
        }

        /// <summary>
        /// sp_alert_settings_update(...) -- actualiza los umbrales y registra el
        /// cambio en la bitácora de auditoría. Devuelve el estado ya actualizado.
        /// </summary>
        public async Task<AlertSetting?> UpdateAsync(decimal warnLimitHours, decimal turnLimitHours, long userId, string clientIp)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_alert_settings_update", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_warn_limit_hours", warnLimitHours);
                cmd.Parameters.AddWithValue("p_turn_limit_hours", turnLimitHours);
                cmd.Parameters.AddWithValue("p_user_id", userId);
                cmd.Parameters.AddWithValue("p_client_ip", clientIp);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return null;
                }
                return MapSetting(reader);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al actualizar los parámetros de alerta", ex);
            }
        }

        /// <summary>
        /// sp_audit_log_list(p_change_type, p_limit) -- bitácora de auditoría.
        /// </summary>
        public async Task<List<AuditLogItem>> GetAuditLogAsync(string? changeType, int limit)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_audit_log_list", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_change_type", string.IsNullOrWhiteSpace(changeType) ? null : changeType);
                cmd.Parameters.AddWithValue("p_limit", limit);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<AuditLogItem>();
                while (await reader.ReadAsync())
                {
                    result.Add(new AuditLogItem
                    {
                        AuditId = reader.GetInt64("audit_id"),
                        ChangeType = reader.GetString("change_type"),
                        Description = reader.GetString("description"),
                        ChangedByUserId = reader.IsDBNull(reader.GetOrdinal("changed_by_user_id")) ? null : reader.GetInt64("changed_by_user_id"),
                        ChangedByUsername = reader.IsDBNull(reader.GetOrdinal("changed_by_username")) ? null : reader.GetString("changed_by_username"),
                        OldValues = reader.IsDBNull(reader.GetOrdinal("old_values")) ? null : reader.GetString("old_values"),
                        NewValues = reader.IsDBNull(reader.GetOrdinal("new_values")) ? null : reader.GetString("new_values"),
                        ClientIp = reader.IsDBNull(reader.GetOrdinal("client_ip")) ? null : reader.GetString("client_ip"),
                        ChangedAt = reader.GetDateTime("changed_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al listar la bitácora de auditoría", ex);
            }
        }

        private static AlertSetting MapSetting(MySqlDataReader reader)
        {
            return new AlertSetting
            {
                WarnLimitHours = reader.GetDecimal("warn_limit_hours"),
                TurnLimitHours = reader.GetDecimal("turn_limit_hours"),
                UpdatedByUserId = reader.IsDBNull(reader.GetOrdinal("updated_by_user_id")) ? null : reader.GetInt64("updated_by_user_id"),
                UpdatedByName = reader.IsDBNull(reader.GetOrdinal("updated_by_name")) ? null : reader.GetString("updated_by_name"),
                UpdatedAt = reader.GetDateTime("updated_at")
            };
        }
    }
}
