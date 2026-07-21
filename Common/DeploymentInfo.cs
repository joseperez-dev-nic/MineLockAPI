using System;

namespace RampaSegura.Api.Common
{
    /// <summary>
    /// Dice DÓNDE está corriendo esta instancia de la API: en el servidor de la
    /// mina ("Local") o en la nube ("Cloud"). Se decide con appsettings
    /// "Deployment:Mode"; el código y el binario son idénticos en ambos lados.
    ///
    /// - Local: los endpoints de negocio usan la base LOCAL, y el módulo de
    ///   sincronización (local -> nube) está activo.
    /// - Cloud: los endpoints de negocio usan la base de la NUBE, y los endpoints
    ///   de sincronización responden 404 (no existen allá).
    /// </summary>
    public sealed class DeploymentInfo
    {
        public string Mode { get; }
        public bool IsLocal { get; }

        public DeploymentInfo(string? mode)
        {
            // Por seguridad, cualquier valor desconocido se trata como "Cloud":
            // así, si alguien olvida configurarlo en la nube, NO se exponen los
            // endpoints de sync (que necesitan la base local).
            Mode = string.IsNullOrWhiteSpace(mode) ? "Cloud" : mode.Trim();
            IsLocal = Mode.Equals("Local", StringComparison.OrdinalIgnoreCase);
        }
    }
}
