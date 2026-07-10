using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Fila de person_photo (la imagen va como bytes en photo_data / LONGBLOB).
    /// person_photo no tiene is_synced, así que por ahora el sync envía TODAS las
    /// fotos (upsert por photo_id) en cada llamada.
    /// </summary>
    public class PhotoSyncItem
    {
        public long PhotoId { get; set; }
        public long PersonId { get; set; }
        public byte[]? PhotoData { get; set; }
        public int? FileSize { get; set; }
        public string? MimeType { get; set; }
        public DateTime? SyncedFromNcheck { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
