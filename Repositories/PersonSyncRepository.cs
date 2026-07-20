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
    /// Sincronización de la tabla person: LOCAL -> NUBE (incremental).
    /// Solo envía personas con is_synced = 0 (que sp_person_sync_from_ncheck marca
    /// SOLO cuando cambia algún dato real) y tras subirlas las marca is_synced = 1.
    /// </summary>
    public class PersonSyncRepository
    {
        private readonly IRampaSeguraLocalConnectionFactory _local;
        private readonly IRampaSeguraCloudConnectionFactory _cloud;

        public PersonSyncRepository(
            IRampaSeguraLocalConnectionFactory local,
            IRampaSeguraCloudConnectionFactory cloud)
        {
            _local = local;
            _cloud = cloud;
        }

        /// <summary>
        /// sp_person_sync_pending() -- personas con is_synced = 0.
        /// </summary>
        public async Task<List<PersonSyncItem>> GetPendingLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_person_sync_pending", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<PersonSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new PersonSyncItem
                    {
                        PersonId = reader.GetInt64("person_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FirstName = reader.GetString("first_name"),
                        LastName = reader.GetString("last_name"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        IsActive = reader.GetBoolean("is_active"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al leer las personas pendientes en la base local", ex);
            }
        }

        /// <summary>
        /// Upsert de cada persona en la NUBE (sp_person_sync_upsert), en una transacción.
        /// En la nube queda is_synced = 1 (ya es la copia sincronizada).
        /// </summary>
        public async Task PushToCloudAsync(IReadOnlyList<PersonSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_person_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_person_id", item.PersonId);
                    cmd.Parameters.AddWithValue("p_employee_code", item.EmployeeCode);
                    cmd.Parameters.AddWithValue("p_first_name", item.FirstName);
                    cmd.Parameters.AddWithValue("p_last_name", item.LastName);
                    cmd.Parameters.AddWithValue("p_job_position", (object?)item.JobPosition ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_department", (object?)item.Department ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_is_active", item.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar las personas a la nube", ex);
            }
        }

        /// <summary>
        /// Marca is_synced = 1 en local SOLO de las personas ya enviadas.
        /// sp_person_sync_mark protege contra la condición de carrera: si la persona
        /// fue actualizada (nuevo updated_at) después de leerla, NO la marca, para que
        /// el cambio se vuelva a sincronizar en el próximo ciclo.
        /// </summary>
        public async Task MarkSyncedLocalAsync(IReadOnlyList<PersonSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _local.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_person_sync_mark", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_person_id", item.PersonId);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al marcar las personas como sincronizadas en local", ex);
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
