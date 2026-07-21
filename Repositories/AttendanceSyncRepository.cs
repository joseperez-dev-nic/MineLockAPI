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
    /// Sincronización de attendance_session: LOCAL &lt;-&gt; NUBE (BIDIRECCIONAL).
    ///
    /// Los marcajes nacen en local, pero un cierre MANUAL puede hacerse desde
    /// cualquier lado, así que el sync va en las dos direcciones. Ambas usan el
    /// MISMO mecanismo de bandera is_synced y los MISMOS procedimientos (existen
    /// en las dos bases); solo se invierte cuál conexión es origen y cuál destino:
    ///
    ///   PUSH  local -> nube : lee is_synced=0 en local, upsert en nube, marca local.
    ///   PULL  nube  -> local: lee is_synced=0 en nube,  upsert en local, marca nube.
    ///
    /// El upsert pone is_synced=1 en el destino, por eso no hay ping-pong. No se
    /// compara updated_at entre servidores, así que la diferencia de zona horaria
    /// no afecta (a diferencia de alert_threshold).
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

        // --- Direcciones públicas (nombres claros; abajo, la lógica compartida) ---

        /// <summary>PUSH: lee los marcajes pendientes en LOCAL.</summary>
        public Task<List<SyncPendingItem>> GetPendingLocalAsync(CancellationToken ct = default) =>
            ReadPendingAsync(_local.CreateConnection, "local", ct);

        /// <summary>PULL: lee los marcajes pendientes en la NUBE (p. ej. cierres manuales hechos allá).</summary>
        public Task<List<SyncPendingItem>> GetPendingCloudAsync(CancellationToken ct = default) =>
            ReadPendingAsync(_cloud.CreateConnection, "la nube", ct);

        /// <summary>PUSH: aplica (upsert) los marcajes en la NUBE.</summary>
        public Task PushToCloudAsync(IReadOnlyList<SyncPendingItem> items, CancellationToken ct = default) =>
            UpsertAsync(_cloud.CreateConnection, "la nube", items, ct);

        /// <summary>PULL: aplica (upsert) en LOCAL los marcajes traídos de la nube.</summary>
        public Task ApplyToLocalAsync(IReadOnlyList<SyncPendingItem> items, CancellationToken ct = default) =>
            UpsertAsync(_local.CreateConnection, "local", items, ct);

        /// <summary>PUSH: marca is_synced=1 en LOCAL de lo ya enviado.</summary>
        public Task MarkSyncedLocalAsync(IReadOnlyList<SyncPendingItem> items, CancellationToken ct = default) =>
            MarkAsync(_local.CreateConnection, "local", items, ct);

        /// <summary>PULL: marca is_synced=1 en la NUBE de lo ya traído.</summary>
        public Task MarkSyncedCloudAsync(IReadOnlyList<SyncPendingItem> items, CancellationToken ct = default) =>
            MarkAsync(_cloud.CreateConnection, "la nube", items, ct);

        // --- Lógica compartida (la conexión llega como delegado) ---

        /// <summary>sp_attendance_sync_pending() en la base indicada (filas con is_synced = 0).</summary>
        private static async Task<List<SyncPendingItem>> ReadPendingAsync(Func<MySqlConnection> connFactory, string origen, CancellationToken ct)
        {
            try
            {
                using var cnn = connFactory();
                using var cmd = new MySqlCommand("sp_attendance_sync_pending", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                // Columnas presentes en el result set. Permite que una base que va un
                // paso atrás (procedimiento aún sin las columnas nuevas) NO tumbe el
                // sync: las columnas ausentes simplemente se leen como null/false.
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));

                bool Has(string c) => cols.Contains(c) && !reader.IsDBNull(reader.GetOrdinal(c));

                var result = new List<SyncPendingItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new SyncPendingItem
                    {
                        SessionId = reader.GetInt64("session_id"),
                        PersonId = reader.GetInt64("person_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FullName = Has("full_name") ? reader.GetString("full_name") : null,
                        JobPosition = Has("job_position") ? reader.GetString("job_position") : null,
                        Department = Has("department") ? reader.GetString("department") : null,
                        LevelId = Has("level_id") ? reader.GetInt32("level_id") : (int?)null,
                        EntryTime = reader.GetDateTime("entry_time"),
                        ExitTime = Has("exit_time") ? reader.GetDateTime("exit_time") : (DateTime?)null,
                        TimeZone = Has("time_zone") ? reader.GetInt64("time_zone") : (long?)null,
                        ExitTimeZone = Has("exit_time_zone") ? reader.GetInt64("exit_time_zone") : (long?)null,
                        TimeInside = Has("time_inside") ? reader.GetTimeSpan("time_inside") : (TimeSpan?)null,
                        ClosedManually = Has("closed_manually") && reader.GetBoolean("closed_manually"),
                        ClosedByUserId = Has("closed_by_user_id") ? reader.GetInt64("closed_by_user_id") : (long?)null,
                        ClosedReason = Has("closed_reason") ? reader.GetString("closed_reason") : null,
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"Error al leer los marcajes pendientes en {origen}", ex);
            }
        }

        /// <summary>
        /// sp_attendance_sync_upsert(...) por cada fila, en transacción. Pone is_synced=1
        /// en el destino (por eso no hay ping-pong). Columnas generadas quedan fuera.
        /// </summary>
        private static async Task UpsertAsync(Func<MySqlConnection> connFactory, string destino, IReadOnlyList<SyncPendingItem> items, CancellationToken ct)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = connFactory();
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
                    cmd.Parameters.AddWithValue("p_time_zone", (object?)item.TimeZone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_exit_time_zone", (object?)item.ExitTimeZone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_closed_manually", item.ClosedManually ? 1 : 0);
                    cmd.Parameters.AddWithValue("p_closed_by_user_id", (object?)item.ClosedByUserId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_closed_reason", (object?)item.ClosedReason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_created_at", item.CreatedAt);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"Error al aplicar los marcajes en {destino}", ex);
            }
        }

        /// <summary>
        /// sp_attendance_sync_mark(p_session_id, p_updated_at) por cada fila. Marca
        /// is_synced=1 solo si no fue tocada tras leerla (protección de carrera, dentro
        /// de la MISMA base, así que no hay problema de zona horaria).
        /// </summary>
        private static async Task MarkAsync(Func<MySqlConnection> connFactory, string origen, IReadOnlyList<SyncPendingItem> items, CancellationToken ct)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = connFactory();
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
                throw new DataAccessException((int)ex.Number, $"Error al marcar los marcajes como sincronizados en {origen}", ex);
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
