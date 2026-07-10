namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Foto de perfil de un empleado para exportar a disco (assets/profile-photos).
    /// PhotoData viaja en base64; el nombre del archivo será {EmployeeCode}.{ext}.
    /// </summary>
    public class ProfilePhotoExport
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string PhotoData { get; set; } = string.Empty; // base64
        public string? MimeType { get; set; }
    }
}
