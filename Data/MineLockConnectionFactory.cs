using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;

namespace MineLock.Api.Data
{
    public interface IMineLockConnectionFactory
    {
        MySqlConnection CreateConnection();
    }

    /// <summary>
    /// Registrar como Singleton en Program.cs. No abre la conexión, solo la construye;
    /// cada repository es responsable de abrir/cerrar (using) su propia conexión,
    /// igual que en tu patrón actual con SqlConnectionFactory.
    /// </summary>
    public class MineLockConnectionFactory : IMineLockConnectionFactory
    {
        private readonly string _connectionString;

        public MineLockConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("MineLock")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'MineLock'.");
        }

        public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);
    }
}
