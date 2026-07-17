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
    /// Sincronización de alert_threshold_setting: LOCAL -> NUBE.
    /// Tabla mínima sin is_synced: se hace upsert de todas las filas (por setting_id)
    /// en cada llamada. La nube necesita estos límites porque sp_warning_report
    /// (que corre allá) los lee para calcular nivel_alerta.
    /// </summary>
    public class AlertThresholdSyncRepository
    {
        private readonly IRampaSeguraLocalConnectionFactory _local;
        private readonly IRampaSeguraCloudConnectionFactory _cloud;

        public AlertThresholdSyncRepository(
            IRampaSeguraLocalConnectionFactory local,
            IRampaSeguraCloudConnectionFactory cloud)
        {
            _local = local;
            _cloud = cloud;
        }

        /// <summary>
        /// sp_alertthreshold_sync_source() -- todos los límites configurados en local.
        /// </summary>
        public async Task<List<AlertThresholdSyncItem>> GetSourceLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_alertthreshold_sync_source", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<AlertThresholdSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new AlertThresholdSyncItem
                    {
                        // setting_id es TINYINT UNSIGNED: se lee con GetValue + Convert
                        // para no depender de cómo lo mapee el driver (byte/sbyte).
                        SettingId = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("setting_id"))),
                        WarnLimitHours = reader.IsDBNull(reader.GetOrdinal("warn_limit_hours")) ? null : reader.GetDecimal("warn_limit_hours"),
                        TurnLimitHours = reader.IsDBNull(reader.GetOrdinal("turn_limit_hours")) ? null : reader.GetDecimal("turn_limit_hours"),
                        UpdatedByUserId = reader.IsDBNull(reader.GetOrdinal("updated_by_user_id")) ? null : reader.GetInt64("updated_by_user_id"),
                        UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al leer los límites de alerta en la base local", ex);
            }
        }

        /// <summary>
        /// Upsert de cada límite en la NUBE (sp_alertthreshold_sync_upsert), en transacción.
        /// </summary>
        public async Task PushToCloudAsync(IReadOnlyList<AlertThresholdSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_alertthreshold_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_setting_id", item.SettingId);
                    cmd.Parameters.AddWithValue("p_warn_limit_hours", (object?)item.WarnLimitHours ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_turn_limit_hours", (object?)item.TurnLimitHours ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_updated_by_user_id", (object?)item.UpdatedByUserId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_updated_at", (object?)item.UpdatedAt ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar los límites de alerta a la nube", ex);
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
