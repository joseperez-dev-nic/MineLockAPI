using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de la tabla person para sincronizar a la nube (incremental).
    /// Solo se envían las que tienen is_synced = 0, que sp_person_sync_from_ncheck
    /// marca SOLO cuando cambia algún dato real (no en cada toque).
    /// updated_at se usa para proteger contra la condición de carrera al marcar.
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
        public DateTime UpdatedAt { get; set; }
    }
}
