namespace DockerizeAPI.Models.Configuration;

/// <summary>
/// Configuracion general del sistema de build.
/// Se carga desde la seccion "Build" del appsettings.json.
/// </summary>
public class BuildSettings
{
    /// <summary>
    /// Nombre de la seccion en appsettings.json.
    /// </summary>
    public const string SectionName = "Build";

    /// <summary>
    /// Plataforma/arquitectura por defecto para las imagenes. Default: "linux/amd64".
    /// </summary>
    public string DefaultPlatform { get; set; } = "linux/amd64";

    /// <summary>
    /// Runtime identifier de .NET. Default: "linux-x64".
    /// </summary>
    public string DefaultRuntime { get; set; } = "linux-x64";

    /// <summary>
    /// Imagenes base Alpine (sin ODBC) para builds ligeros.
    /// </summary>
    public BaseImages AlpineBaseImages { get; set; } = new()
    {
        Sdk = "repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine",
        Aspnet = "repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine"
    };

    /// <summary>
    /// Imagenes base Debian (con ODBC) para builds que necesitan drivers ODBC.
    /// </summary>
    public BaseImages DebianBaseImages { get; set; } = new()
    {
        Sdk = "repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0",
        Aspnet = "repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0"
    };

    /// <summary>
    /// Maximo de builds simultaneos. Default: 3.
    /// </summary>
    public int MaxConcurrentBuilds { get; set; } = 3;

    /// <summary>
    /// Timeout maximo por build en minutos. Default: 10.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Directorio base para archivos temporales de build.
    /// </summary>
    public string TempDirectory { get; set; } = "/tmp/dockerize-builds";
}

/// <summary>
/// Par de imagenes base (SDK para compilar, ASP.NET para ejecutar).
/// </summary>
public class BaseImages
{
    /// <summary>
    /// Imagen SDK con herramientas de compilacion de .NET.
    /// </summary>
    public string Sdk { get; set; } = string.Empty;

    /// <summary>
    /// Imagen ASP.NET runtime ligera para ejecutar la aplicacion.
    /// </summary>
    public string Aspnet { get; set; } = string.Empty;
}
