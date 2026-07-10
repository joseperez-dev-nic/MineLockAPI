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
    public class PersonRepository
    {
        private readonly IRampaSeguraConnectionFactory _factory;

        public PersonRepository(IRampaSeguraConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// sp_ncheck_person_list() -- sin parámetros. Personal maestro desde
        /// ncheck_db.person (is_deleted = 0), ordenado por person_id.
        /// </summary>
        public async Task<List<Person>> GetPersonListAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_ncheck_person_list", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<Person>();
                while (await reader.ReadAsync())
                {
                    result.Add(new Person
                    {
                        PersonId = reader.GetInt64("person_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FirstName = reader.GetString("first_name"),
                        LastName = reader.GetString("last_name"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al listar el personal de NCHECK", ex);
            }
        }

        /// <summary>
        /// sp_person_list() -- sin parámetros. Personal activo (is_active = 1)
        /// de la tabla local `person`, no de ncheck_db.Person
        /// </summary>
        public async Task<List<Person>> GetLBPersonListAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_person_list", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<Person>();
                while (await reader.ReadAsync())
                {
                    result.Add(new Person
                    {
                        PersonId = reader.GetInt64("person_id"),
                        EmployeeCode = reader.GetString("employee_code"),
                        FirstName = reader.GetString("first_name"),
                        LastName = reader.GetString("last_name"),
                        Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                        JobPosition = reader.IsDBNull(reader.GetOrdinal("job_position")) ? null : reader.GetString("job_position")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al listar el personal local", ex);
            }
        }

        /// <summary>
        /// sp_person_photos_all() -- sin parámetros. Todos los empleados activos que
        /// tienen foto, con su employee_code y la imagen en base64. Se usa para
        /// exportar las fotos a disco (assets/profile-photos) al arrancar la app.
        /// </summary>
        public async Task<List<ProfilePhotoExport>> GetAllProfilePhotosAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_person_photos_all", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                var result = new List<ProfilePhotoExport>();
                while (await reader.ReadAsync())
                {
                    var photoOrdinal = reader.GetOrdinal("photo_data");
                    if (reader.IsDBNull(photoOrdinal)) continue;

                    var photoBytes = (byte[])reader.GetValue(photoOrdinal);
                    result.Add(new ProfilePhotoExport
                    {
                        EmployeeCode = reader.GetString("employee_code"),
                        PhotoData = Convert.ToBase64String(photoBytes),
                        MimeType = reader.IsDBNull(reader.GetOrdinal("mime_type")) ? null : reader.GetString("mime_type")
                    });
                }
                return result;
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al obtener las fotos de perfil de los empleados", ex);
            }
        }

        /// <summary>
        /// sp_person_sync_all_from_ncheck() -- sin parámetros. INSERT ... ON DUPLICATE KEY UPDATE
        /// de ncheck_db.person hacia la tabla local `person`. No devuelve result set,
        /// solo el conteo de filas afectadas (MySQL cuenta cada UPDATE como 2 filas).
        /// </summary>
        public async Task<int> SyncAllFromNcheckAsync()
        {
            try
            {
                using var cnn = _factory.CreateConnection();
                using var cmd = new MySqlCommand("sp_person_sync_all_from_ncheck", cnn);
                cmd.CommandType = CommandType.StoredProcedure;

                await cnn.OpenAsync();
                return await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                throw new DataAccessException((int)ex.Number, "Error al sincronizar el personal desde NCHECK", ex);
            }
        }
    }
}
