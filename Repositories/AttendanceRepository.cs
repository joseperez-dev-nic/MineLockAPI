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
    public class AttendanceRepository
    {
        private readonly IMineLockConnectionFactory _factory;

        public AttendanceRepository(IMineLockConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_session_open(p_employee_code, p_level_code, p_entry_time).
        /// Señaliza SQLSTATE '45000' con PERSON_NOT_FOUND, LEVEL_NOT_FOUND o
        /// ALREADY_INSIDE si la coherencia falla. Ese texto queda en ex.Message
        /// y se propaga en el mensaje de la excepción para que el consumidor
        /// (Candados, o quien pruebe la API) sepa exactamente qué regla falló.
        /// </summary>
        public async Task<SessionOpenResult?> OpenSessionAsync(long personId, int levelId, DateTime? entryTime)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_session_open", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_person_id", personId);
                cmd.Parameters.AddWithValue("p_level_id", levelId);
                cmd.Parameters.AddWithValue("p_entry_time", entryTime ?? DateTime.Now);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new SessionOpenResult
                    {
                        SessionId = reader.GetInt64("session_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FirstName = reader.GetString("first_name"),
                        LastName = reader.GetString("last_name"),
                        LevelCode = reader.IsDBNull(reader.GetOrdinal("level_code")) ? null : reader.GetString("level_code"),
                        LevelName = reader.IsDBNull(reader.GetOrdinal("level_name")) ? null : reader.GetString("level_name"),
                        EntryTime = reader.GetDateTime("entry_time")
                    };
                }
                return null;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"{ex.Message}", ex);
            }
        }

        /// <summary>
        /// sp_session_close(p_employee_code, p_exit_time).
        /// Señaliza PERSON_NOT_FOUND o NOT_INSIDE si la coherencia falla.
        /// </summary>
        public async Task<SessionCloseResult?> CloseSessionAsync(long personId, DateTime? exitTime)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_session_close", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_person_id", personId);
                cmd.Parameters.AddWithValue("p_exit_time", exitTime ?? DateTime.Now);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new SessionCloseResult
                    {
                        SessionId = reader.GetInt64("session_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FirstName = reader.GetString("first_name"),
                        LastName = reader.GetString("last_name"),
                        EntryTime = reader.GetDateTime("entry_time"),
                        ExitTime = reader.GetDateTime("exit_time")
                    };
                }
                return null;
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
