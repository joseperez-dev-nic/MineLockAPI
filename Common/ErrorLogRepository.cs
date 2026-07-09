using RampaSegura.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading.Tasks;

namespace RampaSegura.Api.Common
{
    /// <summary>
    /// Escribe en la base de errores compartida (pa_registrar_error, SQL Server db_errors_log).
    /// A propósito NO relanza excepciones: un fallo al loguear (ej. esa DB está caída)
    /// nunca debe tumbar el request real ni ocultar el error original que sí se le
    /// respondió al cliente. Si falla, solo queda constancia en el log local (ILogger).
    /// </summary>
    public class ErrorLogRepository
    {
        private const string Application = "RampaSeguraAPI";

        private readonly IErrorLogConnectionFactory _factory;
        private readonly ILogger<ErrorLogRepository> _logger;

        public ErrorLogRepository(IErrorLogConnectionFactory factory, ILogger<ErrorLogRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task RegisterAsync(string module, int errorNumber, string message, string clientIp, string user)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new SqlCommand("pa_registrar_error", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@aplicacion", Application);
                cmd.Parameters.AddWithValue("@modulo", module);
                cmd.Parameters.AddWithValue("@num_error", errorNumber);
                cmd.Parameters.AddWithValue("@mensaje", message);
                cmd.Parameters.AddWithValue("@ip_cliente", clientIp);
                cmd.Parameters.AddWithValue("@usuario", user);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar el error en la base de errores compartida");
            }
        }
    }
}
