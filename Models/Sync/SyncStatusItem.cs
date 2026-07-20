using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Foto del estado de attendance_session en UNA base (local o nube).
    /// </summary>
    public class SyncStatusItem
    {
        /// <summary>MAX(updated_at) de attendance_session: cuándo se tocó un marcaje por última vez.</summary>
        public DateTime? UltimaActualizacion { get; set; }

        /// <summary>Total de marcajes en esa base.</summary>
        public int Total { get; set; }

        /// <summary>Marcajes con is_synced = 0. En la nube siempre debería ser 0.</summary>
        public int Pendientes { get; set; }

        /// <summary>Último sync_log SUCCESS de tipo ATTENDANCE.</summary>
        public DateTime? UltimaSincronizacion { get; set; }
    }

    /// <summary>
    /// Estado de la sincronización de asistencias: compara local contra nube.
    /// Si Total coincide y Pendientes = 0, todo está al día.
    /// </summary>
    public class AttendanceSyncStatus
    {
        public SyncStatusItem Local { get; set; } = new();

        /// <summary>null si no se pudo consultar la nube (ver NubeError).</summary>
        public SyncStatusItem? Nube { get; set; }

        /// <summary>Motivo por el que no se pudo leer la nube (sin internet, etc.).</summary>
        public string? NubeError { get; set; }

        /// <summary>
        /// true cuando no quedan pendientes en local y la nube tiene el mismo total.
        /// Es la respuesta rápida a "¿está todo sincronizado?".
        /// </summary>
        public bool AlDia => Nube is not null && Local.Pendientes == 0 && Local.Total == Nube.Total;
    }
}
