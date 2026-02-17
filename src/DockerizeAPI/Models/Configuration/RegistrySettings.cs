namespace DockerizeAPI.Models.Configuration;

/// <summary>
/// Configuracion del Container Registry de Gitea.
/// Se carga desde la seccion "Registry" del appsettings.json.
/// </summary>
public class RegistrySettings
{
    /// <summary>
    /// Nombre de la seccion en appsettings.json.
    /// </summary>
    public const string SectionName = "Registry";

    /// <summary>
    /// URL del Container Registry de Gitea. Default: "repos.daviviendahn.dvhn".
    /// </summary>
    public string Url { get; set; } = "repos.daviviendahn.dvhn";

    /// <summary>
    /// Organizacion/owner en Gitea. Default: "davivienda-banco".
    /// </summary>
    public string Owner { get; set; } = "davivienda-banco";
}
