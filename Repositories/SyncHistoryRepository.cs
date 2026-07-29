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
    /// Historial de sync_log sobre la base OPERATIVA (local o nube según el
    /// despliegue). A diferencia de SyncStatusRepository (que compara local
    /// contra nube y solo existe en el despliegue Local), este repositorio
    /// usa la conexión operativa -- registrada en los dos despliegues -- así
    /// que el endpoint funciona tanto en local como en la nube.
    /// </summary>
    public class SyncHistoryRepository
    {
        private readonly IRampaSeguraConnectionFactory _factory;

        public SyncHistoryRepository(IRampaSeguraConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_sync_history(p_sync_type, p_fecha_desde, p_fecha_hasta, p_limit)
        /// -- historial de sync_log en la base operativa, del más reciente al más viejo.
        /// Los tres primeros parámetros son opcionales (NULL = sin filtrar).
        /// </summary>
        public async Task<List<SyncLogSyncItem>> GetHistoryAsync(
            string? syncType,
            DateOnly? fechaDesde,
            DateOnly? fechaHasta,
            int limit,
            CancellationToken ct = default)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_sync_history", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("p_sync_type", string.IsNullOrWhiteSpace(syncType) ? DBNull.Value : syncType.ToUpperInvariant());
                cmd.Parameters.AddWithValue("p_fecha_desde", (object?)fechaDesde ?? DBNull.Value);
                cmd.Parameters.AddWithValue("p_fecha_hasta", (object?)fechaHasta ?? DBNull.Value);
                cmd.Parameters.AddWithValue("p_limit", limit);

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
                throw new DataAccessException((int)ex.Number, "Error al consultar el historial de sincronizaciones", ex);
            }
        }
    }
}
