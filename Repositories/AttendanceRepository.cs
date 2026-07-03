using RampaSegura.Api.Common;
using RampaSegura.Api.Data;
using RampaSegura.Api.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RampaSegura.Api.Repositories
{
    public class AttendanceRepository
    {
        private readonly IRampaSeguraConnectionFactory _factory;

        public AttendanceRepository(IRampaSeguraConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_session_open(p_person_id, p_level_id, p_entry_time).
        /// Señaliza SQLSTATE '45000' con PERSON_NOT_FOUND, LEVEL_NOT_FOUND o
        /// ALREADY_INSIDE si la coherencia falla. Ese texto queda en ex.Message.
        /// Usa ExecuteNonQueryAsync en vez de leer el SELECT final: el controller
        /// no usa el detalle de la sesión, solo le importa si tuvo éxito o no,
        /// así que no hace falta parsear columnas ni lidiar con los result sets
        /// que agrega la llamada anidada a sp_person_sync_from_ncheck.
        /// </summary>
        public async Task OpenSessionAsync(long personId, int levelId, DateTime? entryTime)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_session_open", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_person_id", personId);
                cmd.Parameters.AddWithValue("p_level_id", levelId);
                cmd.Parameters.AddWithValue("p_entry_time", entryTime);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"{ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// sp_session_close(p_person_id, p_exit_time).
        /// Señaliza PERSON_NOT_FOUND o NOT_INSIDE si la coherencia falla.
        /// </summary>
        public async Task CloseSessionAsync(long personId, DateTime? exitTime)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_session_close", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_person_id", personId);
                cmd.Parameters.AddWithValue("p_exit_time", exitTime);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"{ex.Message}", ex);
            }
        }

        /// <summary>
        /// sp_dashboard_active() -- sin parámetros. Personal dentro de la mina ahora mismo.
        /// </summary>
        public async Task<List<DashboardActiveItem>> GetDashboardActiveAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_dashboard_active", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<DashboardActiveItem>();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashboardActiveItem
                    {
                        SessionId = reader.GetInt64("session_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FullName = reader.GetString("full_name"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position"),
                        LevelId = reader.GetInt32("level_id"),
                        LevelName = reader.IsDBNull(reader.GetOrdinal("level_name")) ? null : reader.GetString("level_name"),
                        EntryTime = reader.GetDateTime("entry_time")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al obtener el dashboard de personal activo", ex);
            }
        }
    }
}
