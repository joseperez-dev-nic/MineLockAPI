using System;

namespace RampaSegura.Api.Models.Sync
{
    /// <summary>
    /// Fila de alert_threshold_setting (límites de advertencia y turno que usa
    /// sp_warning_report). No tiene is_synced y es una tabla mínima, así que se
    /// hace upsert de todas las filas por setting_id.
    /// </summary>
    public class AlertThresholdSyncItem
    {
        public int SettingId { get; set; }
        public decimal? WarnLimitHours { get; set; }
        public decimal? TurnLimitHours { get; set; }
        public long? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
