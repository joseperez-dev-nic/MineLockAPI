using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace RampaSegura.Api.Repositories
{
    /// <summary>
    /// Replica la tabla sync_log: LOCAL -> NUBE.
    /// sync_log no tiene is_synced, por eso se hace upsert de todas las filas
    /// (por sync_id) en cada llamada. A diferencia de los otros syncs, este NO
    /// escribe en sync_log (evita el "log del log").
    /// </summary>
    public class SyncLogSyncRepository
    {
        private readonly IRampaSeguraLocalConnectionFactory _local;
        private readonly IRampaSeguraCloudConnectionFactory _cloud;

        public SyncLogSyncRepository(
            IRampaSeguraLocalConnectionFactory local,
            IRampaSeguraCloudConnectionFactory cloud)
        {
            _local = local;
            _cloud = cloud;
        }

        /// <summary>
        /// sp_synclog_sync_source() -- todas las filas de sync_log en local.
        /// </summary>
        public async Task<List<SyncLogSyncItem>> GetSourceLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_synclog_sync_source", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<SyncLogSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new SyncLogSyncItem
                    {
                        SyncId = reader.GetInt64("sync_id"),
                        StartedAt = reader.GetDateTime("started_at"),
                        FinishedAt = reader.IsDBNull(reader.GetOrdinal("finished_at")) ? null : reader.GetDateTime("finished_at"),
                        Status = reader.IsDBNull(reader.GetOrdinal("status")) ? null : reader.GetString("status"),
                        SyncType = reader.IsDBNull(reader.GetOrdinal("sync_type")) ? null : reader.GetString("sync_type"),
                        RowsSent = reader.IsDBNull(reader.GetOrdinal("rows_sent")) ? null : reader.GetInt32("rows_sent"),
                        ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message")) ? null : reader.GetString("error_message"),
                        CreatedAt = reader.GetDateTime("created_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al leer sync_log en la base local", ex);
            }
        }

        /// <summary>
        /// Upsert de cada fila de sync_log en la NUBE (sp_synclog_sync_upsert), en transacción.
        /// </summary>
        public async Task PushToCloudAsync(IReadOnlyList<SyncLogSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_synclog_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_sync_id", item.SyncId);
                    cmd.Parameters.AddWithValue("p_started_at", item.StartedAt);
                    cmd.Parameters.AddWithValue("p_finished_at", (object?)item.FinishedAt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_status", (object?)item.Status ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_sync_type", (object?)item.SyncType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_rows_sent", (object?)item.RowsSent ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_error_message", (object?)item.ErrorMessage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_created_at", item.CreatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar sync_log a la nube", ex);
            }
        }
    }
}
