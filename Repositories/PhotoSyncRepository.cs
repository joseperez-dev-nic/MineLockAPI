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
    /// Sincronización de person_photo: LOCAL -> NUBE (incremental).
    /// Solo envía fotos con is_synced = 0 y tras subirlas las marca is_synced = 1.
    /// Cada foto viaja como bytes (LONGBLOB).
    /// OJO: person_photo.person_id referencia person(person_id); la persona debe
    /// existir en la nube antes que su foto (sincroniza person primero).
    /// </summary>
    public class PhotoSyncRepository
    {
        private readonly IRampaSeguraLocalConnectionFactory _local;
        private readonly IRampaSeguraCloudConnectionFactory _cloud;

        public PhotoSyncRepository(
            IRampaSeguraLocalConnectionFactory local,
            IRampaSeguraCloudConnectionFactory cloud)
        {
            _local = local;
            _cloud = cloud;
        }

        /// <summary>
        /// sp_photo_sync_pending() -- fotos con is_synced = 0.
        /// </summary>
        public async Task<List<PhotoSyncItem>> GetPendingLocalAsync(CancellationToken ct = default)
        {
            try
            {
                using var cnn = _local.CreateConnection();
                using var cmd = new MySqlCommand("sp_photo_sync_pending", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await cnn.OpenAsync(ct);
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var result = new List<PhotoSyncItem>();
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new PhotoSyncItem
                    {
                        PhotoId = reader.GetInt64("photo_id"),
                        PersonId = reader.GetInt64("person_id"),
                        PhotoData = reader.IsDBNull(reader.GetOrdinal("photo_data")) ? null : reader.GetFieldValue<byte[]>("photo_data"),
                        FileSize = reader.IsDBNull(reader.GetOrdinal("file_size")) ? null : reader.GetInt32("file_size"),
                        MimeType = reader.IsDBNull(reader.GetOrdinal("mime_type")) ? null : reader.GetString("mime_type"),
                        SyncedFromNcheck = reader.IsDBNull(reader.GetOrdinal("synced_from_ncheck")) ? null : reader.GetDateTime("synced_from_ncheck"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al leer las fotos pendientes en la base local", ex);
            }
        }

        /// <summary>
        /// Upsert de cada foto en la NUBE (sp_photo_sync_upsert), en una transacción.
        /// </summary>
        public async Task PushToCloudAsync(IReadOnlyList<PhotoSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _cloud.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_photo_sync_upsert", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_photo_id", item.PhotoId);
                    cmd.Parameters.AddWithValue("p_person_id", item.PersonId);
                    cmd.Parameters.AddWithValue("p_photo_data", (object?)item.PhotoData ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_file_size", (object?)item.FileSize ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_mime_type", (object?)item.MimeType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_synced_from_ncheck", (object?)item.SyncedFromNcheck ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("p_created_at", item.CreatedAt);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al enviar las fotos a la nube", ex);
            }
        }

        /// <summary>
        /// Marca is_synced = 1 en local SOLO de las fotos ya enviadas.
        /// sp_photo_sync_mark protege contra la condición de carrera: si la foto
        /// fue tocada (nuevo updated_at) después de leerla, NO la marca.
        /// </summary>
        public async Task MarkSyncedLocalAsync(IReadOnlyList<PhotoSyncItem> items, CancellationToken ct = default)
        {
            if (items.Count == 0) return;

            try
            {
                using var cnn = _local.CreateConnection();
                await cnn.OpenAsync(ct);
                using var tx = await cnn.BeginTransactionAsync(ct);

                foreach (var item in items)
                {
                    using var cmd = new MySqlCommand("sp_photo_sync_mark", cnn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("p_photo_id", item.PhotoId);
                    cmd.Parameters.AddWithValue("p_updated_at", item.UpdatedAt);

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al marcar las fotos como sincronizadas en local", ex);
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
