namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide con sp_ncheck_person_list: personal maestro tal cual vive en
    /// ncheck_db.person (address2/city vienen aliaseados a department/job_position).
    /// </summary>
    public class Person
    {
        public long PersonId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? JobPosition { get; set; }
    }
}
