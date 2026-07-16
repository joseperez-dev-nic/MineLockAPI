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
        /// sp_session_open(p_person_id, p_level_id, p_entry_time, p_utc_offset_seconds).
        /// El offset (segundos) lo manda el cliente y se usa como zona horaria.
        /// Señaliza SQLSTATE '45000' con PERSON_NOT_FOUND, LEVEL_NOT_FOUND,
        /// ALREADY_INSIDE o TIMEZONE_NOT_FOUND si la coherencia falla. Ese texto queda en ex.Message.
        /// Usa ExecuteNonQueryAsync en vez de leer el SELECT final: el controller
        /// no usa el detalle de la sesión, solo le importa si tuvo éxito o no,
        /// así que no hace falta parsear columnas ni lidiar con los result sets
        /// que agrega la llamada anidada a sp_person_sync_from_ncheck.
        /// </summary>
        public async Task OpenSessionAsync(long personId, int levelId, DateTime? entryTime, long utcOffsetSeconds)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_session_open", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_person_id", personId);
                cmd.Parameters.AddWithValue("p_level_id", levelId);
                cmd.Parameters.AddWithValue("p_entry_time", entryTime);
                cmd.Parameters.AddWithValue("p_utc_offset_seconds", utcOffsetSeconds);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, $"{ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// sp_session_close(p_person_id, p_exit_time, p_utc_offset_seconds).
        /// El offset (segundos) lo manda el cliente y se usa como zona horaria.
        /// Señaliza PERSON_NOT_FOUND, NOT_INSIDE o TIMEZONE_NOT_FOUND si la coherencia falla.
        /// </summary>
        public async Task CloseSessionAsync(long personId, DateTime? exitTime, long utcOffsetSeconds)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_session_close", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_person_id", personId);
                cmd.Parameters.AddWithValue("p_exit_time", exitTime);
                cmd.Parameters.AddWithValue("p_utc_offset_seconds", utcOffsetSeconds);

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
        /// Las fotos NO viajan aquí: se sirven aparte como archivos por código de
        /// empleado (public/profile-photos), poblados vía GET /api/person/photos.
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
                    var item = new DashboardActiveItem
                    {
                        SessionId = reader.GetInt64("session_id"),
                        PersonId = reader.GetInt64("person_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FullName = reader.GetString("full_name"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position"),
                        LevelId = reader.GetInt32("level_id"),
                        LevelName = reader.IsDBNull(reader.GetOrdinal("level_name")) ? null : reader.GetString("level_name"),
                        EntryTime = reader.GetDateTime("entry_time"),
                        MinutesInside = reader.GetInt32("minutes_inside"),
                        TiempoDentro = reader.GetString("tiempo_dentro")
                    };
                    result.Add(item);
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al obtener el dashboard de personal activo", ex);
            }
        }

        /// <summary>
        /// sp_session_report(p_fecha_desde, p_fecha_hasta) -- histórico de sesiones
        /// con entry_time dentro del rango [fechaDesde, fechaHasta] (ambos días incluidos).
        /// </summary>
        public async Task<List<SessionReportItem>> GetReportAsync(DateOnly fechaDesde, DateOnly fechaHasta)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_session_report", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_fecha_desde", fechaDesde);
                cmd.Parameters.AddWithValue("p_fecha_hasta", fechaHasta);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<SessionReportItem>();
                while (await reader.ReadAsync())
                {
                    result.Add(new SessionReportItem
                    {
                        SessionId = reader.GetInt64("session_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FullName = reader.GetString("full_name"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        LevelName = reader.IsDBNull(reader.GetOrdinal("level_name")) ? null : reader.GetString("level_name"),
                        EntryTime = reader.GetDateTime("entry_time"),
                        ExitTime = reader.IsDBNull(reader.GetOrdinal("exit_time")) ? null : reader.GetDateTime("exit_time"),
                        TimeInside = reader.IsDBNull(reader.GetOrdinal("time_inside")) ? null : reader.GetTimeSpan("time_inside"),
                        Status = reader.GetString("status")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al generar el reporte de sesiones", ex);
            }
        }

        /// <summary>
        /// sp_warning_report(p_fecha_desde, p_fecha_hasta) -- sesiones ya cerradas que
        /// superaron el límite de advertencia. Los límites (warn/turno) los lee el SP de
        /// alert_threshold_setting y devuelve nivel_alerta. Ambas fechas son opcionales:
        /// el SP las ignora si vienen NULL.
        /// </summary>
        public async Task<List<WarningReportItem>> GetWarningReportAsync(DateOnly? fechaDesde, DateOnly? fechaHasta)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_warning_report", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_fecha_desde", (object?)fechaDesde ?? DBNull.Value);
                cmd.Parameters.AddWithValue("p_fecha_hasta", (object?)fechaHasta ?? DBNull.Value);

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<WarningReportItem>();
                while (await reader.ReadAsync())
                {
                    result.Add(new WarningReportItem
                    {
                        SessionId = reader.GetInt64("session_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FullName = reader.GetString("full_name"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        LevelName = reader.IsDBNull(reader.GetOrdinal("level_name")) ? null : reader.GetString("level_name"),
                        EntryTime = reader.GetDateTime("entry_time"),
                        ExitTime = reader.IsDBNull(reader.GetOrdinal("exit_time")) ? null : reader.GetDateTime("exit_time"),
                        MinutosDentro = reader.GetInt32("minutos_dentro"),
                        Estado = reader.GetString("estado"),
                        NivelAlerta = reader.IsDBNull(reader.GetOrdinal("nivel_alerta")) ? null : reader.GetString("nivel_alerta")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al generar el reporte de advertencias", ex);
            }
        }
    }
}
