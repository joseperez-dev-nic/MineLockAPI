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
    /// Replica las tablas de bitácora LOCAL -> NUBE: sync_log y audit_log.
    /// Ninguna tiene is_synced, por eso se hace upsert de todas las filas
    /// (por sync_id / audit_id) en cada llamada. A diferencia de los otros syncs,
    /// este NO escribe en sync_log (evita el "log del log").
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

        /// <summary>
        /// sp_auditlog_sync_source() -- todas las filas de audit_log en local.
        /// </summary>
        public async Task<List<AuditLogSyncItem>> GetAuditSourceLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_auditlog_sync_source", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<AuditLogSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new AuditLogSyncItem
                    {
                        AuditId = reader.GetInt64("audit_id"),
                        ChangeType = reader.IsDBNull(reader.GetOrdinal("change_type")) ? null : reader.GetString("change_type"),
                        Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString("description"),
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
                throw new DataAccessException((int)ex.Number, "Error al leer audit_log en la base local", ex);
            }
        }

        /// <summary>
        /// Upsert de cada fila de audit_log en la NUBE (sp_auditlog_sync_upsert), en transacción.
        /// </summary>
        public async Task PushAuditToCloudAsync(IReadOnlyList<AuditLogSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_auditlog_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_audit_id", item.AuditId);
                    cmd.Parameters.AddWithValue("p_change_type", (object?)item.ChangeType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_description", (object?)item.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_changed_by_user_id", (object?)item.ChangedByUserId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_changed_by_username", (object?)item.ChangedByUsername ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_old_values", (object?)item.OldValues ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_new_values", (object?)item.NewValues ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_client_ip", (object?)item.ClientIp ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_changed_at", item.ChangedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar audit_log a la nube", ex);
            }
        }

        // --- attendance_session_edit_log: BIDIRECCIONAL ---------------------
        // Las correcciones pueden hacerse en local o en la nube. Gracias al
        // auto_increment_offset (local impares / nube pares) los edit_id nunca
        // chocan, así que basta upsert por edit_id en las dos direcciones: cada
        // base termina con la unión de todas las ediciones.

        /// <summary>Lee edit_log de LOCAL.</summary>
        public Task<List<AttendanceEditLogSyncItem>> GetEditLogLocalAsync(CancellationToken ct = default) =>
            ReadEditLogAsync(_local.CreateConnection, "local", ct);

        /// <summary>Lee edit_log de la NUBE.</summary>
        public Task<List<AttendanceEditLogSyncItem>> GetEditLogCloudAsync(CancellationToken ct = default) =>
            ReadEditLogAsync(_cloud.CreateConnection, "la nube", ct);

        /// <summary>Upsert de edit_log en la NUBE (push local -> nube).</summary>
        public Task PushEditLogToCloudAsync(IReadOnlyList<AttendanceEditLogSyncItem> items, CancellationToken ct = default) =>
            UpsertEditLogAsync(_cloud.CreateConnection, "la nube", items, ct);

        /// <summary>Upsert de edit_log en LOCAL (pull nube -> local).</summary>
        public Task ApplyEditLogToLocalAsync(IReadOnlyList<AttendanceEditLogSyncItem> items, CancellationToken ct = default) =>
            UpsertEditLogAsync(_local.CreateConnection, "local", items, ct);

        /// <summary>sp_editlog_sync_source() en la base indicada.</summary>
        private static async Task<List<AttendanceEditLogSyncItem>> ReadEditLogAsync(Func<MySqlConnection> connFactory, string origen, CancellationToken ct)
        {
            try
            {
                using var cnn = connFactory();
                using var cmd = new MySqlCommand("sp_editlog_sync_source", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<AttendanceEditLogSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new AttendanceEditLogSyncItem
                    {
                        EditId = reader.GetInt64("edit_id"),
                        SessionId = reader.GetInt64("session_id"),
                        EditedByUserId = reader.GetInt64("edited_by_user_id"),
                        EditedAt = reader.GetDateTime("edited_at"),
                        FieldChanged = reader.GetString("field_changed"),
                        OldValue = reader.IsDBNull(reader.GetOrdinal("old_value")) ? null : reader.GetString("old_value"),
                        NewValue = reader.IsDBNull(reader.GetOrdinal("new_value")) ? null : reader.GetString("new_value"),
                        Reason = reader.IsDBNull(reader.GetOrdinal("reason")) ? null : reader.GetString("reason"),
                        IsDeleted = !reader.IsDBNull(reader.GetOrdinal("is_deleted")) && reader.GetBoolean("is_deleted"),
                        DeletedByUserId = reader.IsDBNull(reader.GetOrdinal("deleted_by_user_id")) ? null : reader.GetInt64("deleted_by_user_id"),
                        DeletedAt = reader.IsDBNull(reader.GetOrdinal("deleted_at")) ? null : reader.GetDateTime("deleted_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"Error al leer attendance_session_edit_log en {origen}", ex);
            }
        }

        /// <summary>
        /// sp_editlog_sync_upsert(...) por cada fila, en transacción.
        /// OJO FK: edit_log referencia attendance_session y app_user; ambos deben
        /// existir en el destino antes. Si falla por FK, se reintenta el próximo ciclo.
        /// </summary>
        private static async Task UpsertEditLogAsync(Func<MySqlConnection> connFactory, string destino, IReadOnlyList<AttendanceEditLogSyncItem> items, CancellationToken ct)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = connFactory();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_editlog_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_edit_id", item.EditId);
                    cmd.Parameters.AddWithValue("p_session_id", item.SessionId);
                    cmd.Parameters.AddWithValue("p_edited_by_user_id", item.EditedByUserId);
                    cmd.Parameters.AddWithValue("p_edited_at", item.EditedAt);
                    cmd.Parameters.AddWithValue("p_field_changed", item.FieldChanged);
                    cmd.Parameters.AddWithValue("p_old_value", (object?)item.OldValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_new_value", (object?)item.NewValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_reason", (object?)item.Reason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_is_deleted", item.IsDeleted ? 1 : 0);
                    cmd.Parameters.AddWithValue("p_deleted_by_user_id", (object?)item.DeletedByUserId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_deleted_at", (object?)item.DeletedAt ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"Error al enviar attendance_session_edit_log a {destino}", ex);
            }
        }
    }
}
