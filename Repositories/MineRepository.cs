using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Models;
using MySqlConnector;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RampaSegura.Api.Repositories
{
    public class MineRepository
    {
        private readonly IRampaSeguraConnectionFactory _factory;

        public MineRepository(IRampaSeguraConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<int> InsertAsync(string mineName, string location, string country, string timezoneName, int utcOffsetMinutes)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_mine_insert", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_mine_name", mineName);
                cmd.Parameters.AddWithValue("p_location", location);
                cmd.Parameters.AddWithValue("p_country", country);
                cmd.Parameters.AddWithValue("p_timezone_name", timezoneName);
                cmd.Parameters.AddWithValue("p_utc_offset_minutes", utcOffsetMinutes);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return reader.GetInt32("mine_id");
                }
                return 0;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al insertar la mina", ex);
            }
        }

        public async Task<List<Mine>> ListAsync(bool onlyActive = false)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_mine_list", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_only_active", onlyActive ? 1 : 0);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<Mine>();
                while (await reader.ReadAsync())
                {
                    result.Add(new Mine
                    {
                        MineId = reader.GetInt32("mine_id"),
                        MineName = reader.GetString("mine_name"),
                        Location = reader.GetString("location"),
                        Country = reader.GetString("country"),
                        TimezoneName = reader.GetString("timezone_name"),
                        UtcOffsetMinutes = reader.GetInt32("utc_offset_minutes"),
                        IsActive = reader.GetBoolean("is_active"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al listar las minas", ex);
            }
        }

        public async Task<Mine> GetByIdAsync(int mineId)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_mine_get_by_id", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_mine_id", mineId);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Mine
                    {
                        MineId = reader.GetInt32("mine_id"),
                        MineName = reader.GetString("mine_name"),
                        Location = reader.GetString("location"),
                        Country = reader.GetString("country"),
                        TimezoneName = reader.GetString("timezone_name"),
                        UtcOffsetMinutes = reader.GetInt32("utc_offset_minutes"),
                        IsActive = reader.GetBoolean("is_active"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    };
                }
                return null;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al obtener la mina", ex);
            }
        }

        public async Task UpdateAsync(int mineId, string mineName, string location, string country, string timezoneName, int utcOffsetMinutes)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_mine_update", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_mine_id", mineId);
                cmd.Parameters.AddWithValue("p_mine_name", mineName);
                cmd.Parameters.AddWithValue("p_location", location);
                cmd.Parameters.AddWithValue("p_country", country);
                cmd.Parameters.AddWithValue("p_timezone_name", timezoneName);
                cmd.Parameters.AddWithValue("p_utc_offset_minutes", utcOffsetMinutes);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al actualizar la mina", ex);
            }
        }

        public async Task DeactivateAsync(int mineId)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_mine_deactivate", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_mine_id", mineId);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al desactivar la mina", ex);
            }
        }

        public async Task ActivateAsync(int mineId)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_mine_activate", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("p_mine_id", mineId);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al activar la mina", ex);
            }
        }
    }
}
