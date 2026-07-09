namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con los parámetros de pa_registrar_error en la base de errores
    /// compartida (SQL Server, db_errors_log).
    /// </summary>
    public class ErrorLog
    {
        public string Application { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public int ErrorNumber { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ClientIp { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }
}
