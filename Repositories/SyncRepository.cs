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
    public class SyncRepository
    {
        private readonly IRampaSeguraConnectionFactory _factory;

        public SyncRepository(IRampaSeguraConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_sync_pending() -- sesiones tocadas desde el último sync exitoso
        /// (o todas, si nunca ha corrido un ciclo de sync).
        /// </summary>
        public async Task<List<SyncPendingItem>> GetPendingAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_sync_pending", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<SyncPendingItem>();
                while (await reader.ReadAsync())
                {
                    result.Add(new SyncPendingItem
                    {
                        SessionId = reader.GetInt64("session_id"),
                        PersonId = reader.GetInt64("person_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        LevelId = reader.IsDBNull(reader.GetOrdinal("level_id")) ? null : reader.GetInt32("level_id"),
                        EntryTime = reader.GetDateTime("entry_time"),
                        ExitTime = reader.IsDBNull(reader.GetOrdinal("exit_time")) ? null : reader.GetDateTime("exit_time"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al obtener los registros pendientes de sincronización", ex);
            }
        }

        /// <summary>
        /// sp_sync_start(OUT p_sync_id) -- no devuelve un result set, devuelve
        /// el id nuevo por parámetro de salida. Por eso no usamos ExecuteReaderAsync
        /// aquí, sino ExecuteNonQueryAsync + leer el parámetro después.
        /// </summary>
        public async Task<long> StartSyncAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_sync_start", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                var outParam = new MySqlParameter("p_sync_id", MySqlDbType.UInt64)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outParam);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();

                return Convert.ToInt64(outParam.Value);
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al iniciar el ciclo de sincronización", ex);
            }
        }

        /// <summary>
        /// sp_sync_finish(p_sync_id, p_status, p_rows_sent, p_error).
        /// p_status debe calzar exactamente con el ENUM de sync_log.status
        /// (SUCCESS, FAILED, PARTIAL) -- la validación de eso se hace en el
        /// controller antes de llegar aquí.
        /// </summary>
        public async Task FinishSyncAsync(long syncId, string status, int rowsSent, string? errorMessage)
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_sync_finish", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("p_sync_id", syncId);
                cmd.Parameters.AddWithValue("p_status", status.ToUpperInvariant());
                cmd.Parameters.AddWithValue("p_rows_sent", rowsSent);
                cmd.Parameters.AddWithValue("p_error", (object?)errorMessage ?? DBNull.Value);

                await cnn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al finalizar el ciclo de sincronización", ex);
            }
        }
    }
}
