using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;

namespace RampaSegura.Api.Data
{
    public interface IRampaSeguraConnectionFactory
    {
        MySqlConnection CreateConnection();
    }

    /// <summary>
    /// Registrar como Singleton en Program.cs. No abre la conexión, solo la construye;
    /// cada repository es responsable de abrir/cerrar (using) su propia conexión,
    /// igual que en tu patrón actual con SqlConnectionFactory.
    /// </summary>
    public class RampaSeguraConnectionFactory : IRampaSeguraConnectionFactory
    {
        private readonly string _connectionString;

        public RampaSeguraConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("RampaSegura")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'RampaSegura'.");
        }

        public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);
    }
}
