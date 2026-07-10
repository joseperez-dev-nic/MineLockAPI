using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;

namespace RampaSegura.Api.Data
{
    /// <summary>
    /// Conexión a la base de datos MySQL LOCAL (donde caen los marcajes de Ncheck
    /// con is_synced = 0). Es el ORIGEN de la sincronización local -> nube.
    /// Lee la cadena "RampaSeguraLocal".
    /// </summary>
    public interface IRampaSeguraLocalConnectionFactory
    {
        MySqlConnection CreateConnection();
    }

    public class RampaSeguraLocalConnectionFactory : IRampaSeguraLocalConnectionFactory
    {
        private readonly string _connectionString;

        public RampaSeguraLocalConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("RampaSeguraLocal")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'RampaSeguraLocal'.");
        }

        public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);
    }
}
