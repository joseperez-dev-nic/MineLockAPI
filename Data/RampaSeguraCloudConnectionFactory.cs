using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;

namespace RampaSegura.Api.Data
{
    /// <summary>
    /// Conexión a la base de datos MySQL en la NUBE (espejo de la local).
    /// La sincronización local -> nube usa esta fábrica como destino.
    /// Lee la cadena "RampaSegura" (la nube), la misma que usan los controladores,
    /// pero con un timeout de conexión corto para el ciclo de sync.
    /// </summary>
    public interface IRampaSeguraCloudConnectionFactory
    {
        MySqlConnection CreateConnection();
    }

    /// <summary>
    /// Lee la cadena "RampaSegura" (la nube). Se le fuerza un ConnectTimeout
    /// corto: si no hay internet, queremos que el intento de conexión falle
    /// rápido para no bloquear el ciclo de sync de 3 segundos.
    /// </summary>
    public class RampaSeguraCloudConnectionFactory : IRampaSeguraCloudConnectionFactory
    {
        private readonly string _connectionString;

        public RampaSeguraCloudConnectionFactory(IConfiguration configuration)
        {
            var raw = configuration.GetConnectionString("RampaSegura")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'RampaSegura'.");

            // Aseguramos un timeout de conexión corto aunque no venga en el appsettings.
            var builder = new MySqlConnectionStringBuilder(raw)
            {
                ConnectionTimeout = 5,          // segundos para abrir la conexión
                DefaultCommandTimeout = 10      // segundos por comando
            };
            _connectionString = builder.ConnectionString;
        }

        public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);
    }
}
