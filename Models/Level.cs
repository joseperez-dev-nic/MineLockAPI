namespace RampaSegura.Api.Models
{
    /// <summary>
    /// Coincide exactamente con sp_level_list: solo niveles activos,
    /// y esa columna (is_active) ni siquiera se selecciona.
    /// </summary>
    public class Level
    {
        public int LevelId { get; set; }
        public string LevelCode { get; set; } = string.Empty;
        public string LevelName { get; set; } = string.Empty;
    }
}
