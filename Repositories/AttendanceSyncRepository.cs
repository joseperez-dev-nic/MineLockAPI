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
    /// Sincronización de attendance_session: LOCAL -> NUBE.
    /// Usa dos fábricas de conexión: la local (origen) y la de la nube (destino).
    /// El flujo del ciclo es:
    ///   1) GetPendingLocalAsync   -> lee filas con is_synced = 0 en local
    ///   2) PushToCloudAsync       -> hace upsert de cada fila en la nube (transacción)
    ///   3) MarkSyncedLocalAsync   -> marca is_synced = 1 en local SOLO de lo enviado
    /// Si la nube no responde (sin internet), PushToCloudAsync lanza excepción y
    /// el paso 3 no se ejecuta, así las filas quedan pendientes para el próximo ciclo.
    /// </summary>
    public class AttendanceSyncRepository
    {
        private readonly IRampaSeguraLocalConnectionFactory _local;
        private readonly IRampaSeguraCloudConnectionFactory _cloud;

        public AttendanceSyncRepository(
            IRampaSeguraLocalConnectionFactory local,
            IRampaSeguraCloudConnectionFactory cloud)
        {
            _local = local;
            _cloud = cloud;
        }

        /// <summary>
        /// sp_attendance_sync_pending() -- filas de attendance_session con is_synced = 0.
        /// Devuelve la fila completa (todas las columnas) porque la nube es un espejo.
        /// </summary>
        public async Task<List<SyncPendingItem>> GetPendingLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_attendance_sync_pending", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<SyncPendingItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new SyncPendingItem
                    {
                        SessionId = reader.GetInt64("session_id"),
                        PersonId = reader.GetInt64("person_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FullName = reader.IsDBNull(reader.GetOrdinal("full_name")) ? null : reader.GetString("full_name"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        LevelId = reader.IsDBNull(reader.GetOrdinal("level_id")) ? null : reader.GetInt32("level_id"),
                        EntryTime = reader.GetDateTime("entry_time"),
                        ExitTime = reader.IsDBNull(reader.GetOrdinal("exit_time")) ? null : reader.GetDateTime("exit_time"),
                        TimeInside = reader.IsDBNull(reader.GetOrdinal("time_inside")) ? null : reader.GetTimeSpan("time_inside"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al leer los marcajes pendientes en la base local", ex);
            }
        }

        /// <summary>
        /// Hace upsert de cada fila en la NUBE llamando sp_attendance_sync_upsert.
        /// Todo dentro de una transacción: o entra todo el lote, o no entra nada.
        /// Cualquier fallo aquí (incluida la falta de internet) se propaga como
        /// DataAccessException para que el ciclo lo registre como FAILED.
        /// </summary>
        public async Task PushToCloudAsync(IReadOnlyList<SyncPendingItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_attendance_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_session_id", item.SessionId);
                    cmd.Parameters.AddWithValue("p_person_id", item.PersonId);
                    cmd.Parameters.AddWithValue("p_employee_code", item.EmployeeCode);
                    cmd.Parameters.AddWithValue("p_full_name", (object?)item.FullName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_job_position", (object?)item.JobPosition ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_department", (object?)item.Department ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_level_id", (object?)item.LevelId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_entry_time", item.EntryTime);
                    cmd.Parameters.AddWithValue("p_exit_time", (object?)item.ExitTime ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_created_at", item.CreatedAt);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar los marcajes a la nube", ex);
            }
        }

        /// <summary>
        /// Marca is_synced = 1 en local SOLO de las filas ya enviadas.
        /// sp_attendance_sync_mark protege contra la condición de carrera:
        /// si la fila fue tocada (nuevo updated_at) después de leerla, NO la marca,
        /// para que el cambio se vuelva a sincronizar en el próximo ciclo.
        /// </summary>
        public async Task MarkSyncedLocalAsync(IReadOnlyList<SyncPendingItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _local.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_attendance_sync_mark", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_session_id", item.SessionId);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al marcar los marcajes como sincronizados en local", ex);
            }
        }

        /// <summary>
        /// sp_sync_log_write(p_status, p_rows_sent, p_error) -- inserta una fila
        /// ya finalizada en sync_log (started_at = finished_at = NOW()).
        /// Se escribe en la base LOCAL, que siempre está disponible, incluso sin internet.
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
