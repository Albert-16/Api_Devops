namespace DockerizeAPI.Configuration;

/// <summary>
/// Configuración para los paquetes ODBC necesarios en el template Debian.
/// Incluye la configuración del repositorio Debian privado de Gitea.
/// </summary>
public sealed class OdbcPackagesSettings
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string SectionName = "OdbcPackages";

    /// <summary>URL del repositorio Debian privado en Gitea.</summary>
    public string DebianRepoUrl { get; init; } = string.Empty;

    /// <summary>URL para descargar la clave pública del repositorio Debian.</summary>
    public string DebianKeyUrl { get; init; } = string.Empty;

    /// <summary>Distribución Debian objetivo. Default: bookworm</summary>
    public string Distribution { get; init; } = "bookworm";

    /// <summary>Componente del repositorio Debian. Default: main</summary>
    public string Component { get; init; } = "main";

    /// <summary>Lista de paquetes ODBC a instalar en el contenedor Debian.</summary>
    public List<string> Packages { get; init; } = [];
}
