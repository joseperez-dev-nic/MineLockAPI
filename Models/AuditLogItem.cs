using System;

namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Registro de la bitácora de auditoría (audit_log). Un renglón por cambio:
    /// quién, cuándo, qué tipo de modificación y los valores anterior/nuevo.
    /// </summary>
    public class AuditLogItem
    {
        public long AuditId { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long? ChangedByUserId { get; set; }
        public string? ChangedByUsername { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ClientIp { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
