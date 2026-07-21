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
    /// Sincronización de alert_threshold_setting: LOCAL &lt;-&gt; NUBE (BIDIRECCIONAL).
    /// Los umbrales se pueden editar desde cualquier lado, así que en cada ciclo se
    /// lee de las dos bases y se aplica el merge "gana el más nuevo" (por updated_at)
    /// en ambas. Ese merge ignora lo más viejo, así no hay ping-pong.
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

        /// <summary>Lee los límites de la base LOCAL.</summary>
        public Task<List<AlertThresholdSyncItem>> ReadLocalAsync(CancellationToken ct = default) =>
            ReadAsync(_local.CreateConnection(), "local", ct);

        /// <summary>Lee los límites de la NUBE.</summary>
        public Task<List<AlertThresholdSyncItem>> ReadCloudAsync(CancellationToken ct = default) =>
            ReadAsync(_cloud.CreateConnection(), "la nube", ct);

        /// <summary>Aplica el merge "gana el más nuevo" en la base LOCAL.</summary>
        public Task MergeIntoLocalAsync(IReadOnlyList<AlertThresholdSyncItem> items, CancellationToken ct = default) =>
            MergeAsync(_local.CreateConnection(), "local", items, ct);

        /// <summary>Aplica el merge "gana el más nuevo" en la NUBE.</summary>
        public Task MergeIntoCloudAsync(IReadOnlyList<AlertThresholdSyncItem> items, CancellationToken ct = default) =>
            MergeAsync(_cloud.CreateConnection(), "la nube", items, ct);

        /// <summary>sp_alertthreshold_sync_source() en la base indicada (mismo proc en ambas).</summary>
        private static async Task<List<AlertThresholdSyncItem>> ReadAsync(MySqlConnection connection, string origen, CancellationToken ct)
        {
            try
            {
                using var cnn = connection;
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
                throw new DataAccessException((int)ex.Number, $"Error al leer los límites de alerta en {origen}", ex);
            }
        }

        /// <summary>sp_alertthreshold_merge(...) por cada fila, en transacción.</summary>
        private static async Task MergeAsync(MySqlConnection connection, string destino, IReadOnlyList<AlertThresholdSyncItem> items, CancellationToken ct)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = connection;
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_alertthreshold_merge", cnn, tx)
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
                throw new DataAccessException((int)ex.Number, $"Error al aplicar los límites de alerta en {destino}", ex);
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
