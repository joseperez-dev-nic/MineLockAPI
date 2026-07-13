using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Umbrales globales de advertencia/alerta (en horas) con metadatos de la
    /// última modificación. Corresponde a la fila única de alert_threshold_setting.
    /// </summary>
    public class AlertSetting
    {
        public decimal WarnLimitHours { get; set; }
        public decimal TurnLimitHours { get; set; }
        public long? UpdatedByUserId { get; set; }
        public string? UpdatedByName { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
