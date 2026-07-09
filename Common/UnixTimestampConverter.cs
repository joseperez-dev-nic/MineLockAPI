using System;

namespace RampaSegura.Api.Common
{
    /// <summary>
    /// La app Android manda las fechas como epoch millis (Date.getTime(), tipo long).
    /// Convierte ese long al DateTime local que esperan los stored procedures.
    /// </summary>
    public static class UnixTimestampConverter
    {
        public static DateTime UnixTimestampAFecha(long fechaHora)
        {
            var utc = DateTime.UnixEpoch.AddMilliseconds(fechaHora); 
            return utc.AddHours(-6);
        }
    }
}
