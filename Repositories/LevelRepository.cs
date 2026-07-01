using MineLock.Api.Common;
using MineLock.Api.Data;
using MineLock.Api.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace MineLock.Api.Repositories
{
    public class LevelRepository
    {
        private readonly IMineLockConnectionFactory _factory;

        public LevelRepository(IMineLockConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_level_list() -- sin parámetros. Solo niveles activos.
        /// </summary>
        public async Task<List<Level>> GetAllAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_level_list", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<Level>();
                while (await reader.ReadAsync())
                {
                    result.Add(new Level
                    {
                        LevelId = reader.GetInt32("level_id"),
                        LevelCode = reader.GetString("level_code"),
                        LevelName = reader.GetString("level_name")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al listar los niveles de la mina", ex);
            }
            catch (Exception ex)
            {
                throw new DataAccessException("Error inesperado al listar los niveles", ex);
            }
        }
    }
}
