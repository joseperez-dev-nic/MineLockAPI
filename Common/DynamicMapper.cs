using MySqlConnector;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MineLock.Api.Common
{
    /// <summary>
    /// Convierte el resultado de un MySqlDataReader a JSON genérico (diccionario
    /// columna -> valor), sin necesidad de conocer de antemano los nombres de
    /// columna que devuelve cada stored procedure. Úsalo en cualquier repository
    /// que solo necesite "pasar" lo que el SP devuelve tal cual.
    /// </summary>
    public static class DynamicMapper
    {
        public static async Task<List<Dictionary<string, object?>>> ToListAsync(MySqlDataReader reader)
        {
            var result = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                result.Add(ReadCurrentRow(reader));
            }
            return result;
        }

        public static async Task<Dictionary<string, object?>?> ToSingleAsync(MySqlDataReader reader)
        {
            if (await reader.ReadAsync())
            {
                return ReadCurrentRow(reader);
            }
            return null;
        }

        private static Dictionary<string, object?> ReadCurrentRow(MySqlDataReader reader)
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            return row;
        }
    }
}
