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
    /// Consultas de SOLO LECTURA sobre el estado de la sincronización:
    /// qué tan al día están los marcajes y el historial de sync_log.
    /// No sincroniza nada; sirve para monitorear desde el front o a mano.
    /// </summary>
    public class SyncStatusRepository
    {
        private readonly IRampaSeguraLocalConnectionFactory _local;
        private readonly IRampaSeguraCloudConnectionFactory _cloud;

        public SyncStatusRepository(
            IRampaSeguraLocalConnectionFactory local,
            IRampaSeguraCloudConnectionFactory cloud)
        {
            _local = local;
            _cloud = cloud;
        }

        /// <summary>Estado de attendance_session en la base LOCAL.</summary>
        public Task<SyncStatusItem> GetAttendanceStatusLocalAsync(CancellationToken ct = default) =>
            ReadStatusAsync(_local.CreateConnection(), "local", ct);

        /// <summary>Estado de attendance_session en la NUBE.</summary>
        public Task<SyncStatusItem> GetAttendanceStatusCloudAsync(CancellationToken ct = default) =>
            ReadStatusAsync(_cloud.CreateConnection(), "la nube", ct);

        /// <summary>
        /// sp_attendance_sync_status() -- devuelve una sola fila con el resumen.
        /// El mismo procedimiento existe en las dos bases; solo cambia la conexión.
        /// </summary>
        private static async Task<SyncStatusItem> ReadStatusAsync(MySqlConnection connection, string origen, CancellationToken ct)
        {
            try
            {
                using var cnn = connection;
                using var cmd = new MySqlCommand("sp_attendance_sync_status", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                {
                    return new SyncStatusItem();
                }

                return new SyncStatusItem
                {
                    UltimaActualizacion = reader.IsDBNull(reader.GetOrdinal("ultima_actualizacion")) ? null : reader.GetDateTime("ultima_actualizacion"),
                    Total = reader.GetInt32("total"),
                    Pendientes = reader.GetInt32("pendientes"),
                    UltimaSincronizacion = reader.IsDBNull(reader.GetOrdinal("ultima_sincronizacion")) ? null : reader.GetDateTime("ultima_sincronizacion")
                };
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"Error al consultar el estado de asistencias en {origen}", ex);
            }
        }

        /// <summary>
        /// sp_sync_history(p_sync_type, p_fecha_desde, p_fecha_hasta, p_limit)
        /// -- historial de sync_log en la base LOCAL, del más reciente al más viejo.
        /// Los tres primeros parámetros son opcionales (NULL = sin filtrar).
        /// </summary>
        public async Task<List<SyncLogSyncItem>> GetHistoryLocalAsync(
            string? syncType,
            DateOnly? fechaDesde,
            DateOnly? fechaHasta,
            int limit,
            CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
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
