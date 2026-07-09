using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;

namespace RampaSegura.Api.Data
{
    public interface IErrorLogConnectionFactory
    {
        SqlConnection CreateConnection();
    }

    /// <summary>
    /// Registrar como Singleton en Program.cs. SQL Server aparte (db_errors_log),
    /// compartida con otros proyectos -- solo se usa para pa_registrar_error.
    /// </summary>
    public class ErrorLogConnectionFactory : IErrorLogConnectionFactory
    {
        private readonly string _connectionString;

        public ErrorLogConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("ErrorLogs")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'ErrorLogs'.");
        }

        public SqlConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}
