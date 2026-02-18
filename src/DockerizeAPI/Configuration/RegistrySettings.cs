namespace DockerizeAPI.Configuration;

/// <summary>
/// Configuración del Container Registry de Gitea donde se publican las imágenes.
/// </summary>
public sealed class RegistrySettings
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string SectionName = "Registry";

    /// <summary>URL base del Container Registry (sin protocolo). Ejemplo: repos.daviviendahn.dvhn</summary>
    public string Url { get; init; } = "repos.daviviendahn.dvhn";

    /// <summary>Organización/owner en Gitea bajo la cual se publican las imágenes. Ejemplo: davivienda-banco</summary>
    public string Owner { get; init; } = "davivienda-banco";
}
