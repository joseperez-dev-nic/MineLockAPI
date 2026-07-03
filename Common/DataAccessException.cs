using System;

namespace RampaSegura.Api.Common
{
    /// <summary>
    /// Excepción para envolver cualquier fallo de acceso a datos (MySQL u otro).
    /// Si viene de un SIGNAL SQLSTATE en un stored procedure (validación de coherencia,
    /// p. ej. "salida sin entrada abierta"), MySqlErrorNumber será 1644 (ER_SIGNAL_EXCEPTION).
    /// </summary>
    public class DataAccessException : Exception
    {
        public int? MySqlErrorNumber { get; }

        public DataAccessException(string message, Exception? inner)
            : base(message, inner)
        {
        }

        public DataAccessException(int mySqlErrorNumber, string message, Exception inner)
            : base(message, inner)
        {
            MySqlErrorNumber = mySqlErrorNumber;
        }

        /// <summary>
        /// True si el error viene de un SIGNAL SQLSTATE '45000' dentro de un stored procedure,
        /// es decir, una violación de coherencia de negocio y no un fallo de infraestructura.
        /// </summary>
        public bool IsBusinessRuleViolation => MySqlErrorNumber == 1644;
    }
}
