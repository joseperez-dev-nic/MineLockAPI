namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de la tabla person tal cual vive en la base LOCAL, para sincronizar
    /// a la nube. La tabla person NO tiene is_synced, así que el sync envía el
    /// catálogo completo (upsert por person_id) en cada llamada.
    /// </summary>
    public class PersonSyncItem
    {
        public long PersonId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? JobPosition { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; }
    }
}
