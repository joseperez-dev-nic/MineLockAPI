using MySqlConnector;
using System;

namespace RampaSegura.Api.Data
{
    public interface IRampaSeguraConnectionFactory
    {
        MySqlConnection CreateConnection();
    }

    /// <summary>
    /// Fábrica de la conexión OPERATIVA de esta instancia (la que usan todos los
    /// endpoints de negocio). Recibe ya resuelta la cadena a usar: en el despliegue
    /// Local será la base local; en el despliegue Cloud, la de la nube. Esa decisión
    /// se toma en Program.cs según Deployment:Mode.
    ///
    /// No abre la conexión, solo la construye; cada repository abre/cierra (using)
    /// la suya.
    /// </summary>
    public class RampaSeguraConnectionFactory : IRampaSeguraConnectionFactory
    {
        private readonly string _connectionString;

        public RampaSeguraConnectionFactory(string connectionString)
        {
            _connectionString = connectionString
                ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);
    }
}
