namespace DockerizeAPI.Configuration;

/// <summary>
/// Configuración para el proceso de construcción de imágenes.
/// Incluye plataformas, imágenes base y límites operacionales.
/// </summary>
public sealed class BuildSettings
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string SectionName = "Build";

    /// <summary>Plataforma objetivo para las imágenes. Default: linux/amd64</summary>
    public string DefaultPlatform { get; init; } = "linux/amd64";

    /// <summary>Runtime identifier de .NET. Default: linux-x64</summary>
    public string DefaultRuntime { get; init; } = "linux-x64";

    /// <summary>Imágenes base Alpine (SDK y Runtime) del registry corporativo.</summary>
    public BaseImagePair AlpineBaseImages { get; init; } = new();

    /// <summary>Imágenes base Debian (SDK y Runtime) del registry corporativo.</summary>
    public BaseImagePair DebianBaseImages { get; init; } = new();

    /// <summary>Número máximo de builds ejecutándose simultáneamente. Default: 3</summary>
    public int MaxConcurrentBuilds { get; init; } = 3;

    /// <summary>Timeout máximo por build en minutos. Default: 10</summary>
    public int TimeoutMinutes { get; init; } = 10;

    /// <summary>Directorio temporal para workspaces de build. Default: /tmp/dockerize-builds</summary>
    public string TempDirectory { get; init; } = "/tmp/dockerize-builds";

    /// <summary>
    /// Ruta al directorio con archivos compartidos de soporte para el build
    /// (certificados CA, nuget.config, wget .debs).
    /// Estos archivos se copian al workspace antes del docker build.
    /// Default: /usr/share/containershareds
    /// </summary>
    public string SharedFilesPath { get; init; } = "/usr/share/containershareds";

    /// <summary>
    /// Si true, ejecuta Docker a través de WSL (Windows Subsystem for Linux).
    /// Necesario cuando la API corre en Windows pero Docker está instalado en WSL.
    /// Default: false (ejecuta Docker directamente).
    /// </summary>
    public bool UseWsl { get; init; }

    /// <summary>
    /// Si true, elimina la imagen local después de hacer push al registry.
    /// Útil para ahorrar espacio en disco en servidores de build.
    /// Default: true (limpia imágenes automáticamente).
    /// </summary>
    public bool CleanupAfterPush { get; init; } = true;
}

/// <summary>
/// Par de imágenes base SDK/Runtime para un tipo de distribución.
/// </summary>
public sealed class BaseImagePair
{
    /// <summary>Imagen SDK completa para compilación. Ejemplo: repos.daviviendahn.dvhn/davivienda-banco/dotnet-sdk:10.0-alpine</summary>
    public string Sdk { get; init; } = string.Empty;

    /// <summary>Imagen ASP.NET Runtime ligera para ejecución. Ejemplo: repos.daviviendahn.dvhn/davivienda-banco/dotnet-aspnet:10.0-alpine</summary>
    public string Aspnet { get; init; } = string.Empty;
}
