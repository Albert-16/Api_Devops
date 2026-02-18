using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Entities;

/// <summary>
/// Entidad principal que representa un build de imagen Docker.
/// Almacena el estado completo del ciclo de vida del build, incluyendo
/// la configuración original para soportar retry.
/// </summary>
public sealed class BuildRecord
{
    /// <summary>Identificador único del build.</summary>
    public Guid Id { get; set; }

    /// <summary>URL del repositorio Gitea que se clonó.</summary>
    public required string RepositoryUrl { get; set; }

    /// <summary>Rama del repositorio que se construyó.</summary>
    public required string Branch { get; set; }

    /// <summary>Token de acceso a Gitea (también usado como REPO_TOKEN para paquetes Debian y login al registry).</summary>
    public required string GitToken { get; set; }

    /// <summary>SHA del commit que se construyó (se obtiene después del clone).</summary>
    public string? CommitSha { get; set; }

    /// <summary>Nombre de la imagen sin tag. Ejemplo: ms23-autenticacion-web</summary>
    public required string ImageName { get; set; }

    /// <summary>Tag de la imagen. Ejemplo: sapp-dev</summary>
    public required string ImageTag { get; set; }

    /// <summary>Si es true, usa template Debian con drivers ODBC. Si es false, usa Alpine.</summary>
    public bool IncludeOdbcDependencies { get; set; }

    /// <summary>Estado actual del build en su ciclo de vida.</summary>
    public BuildStatus Status { get; set; } = BuildStatus.Queued;

    /// <summary>Mensaje de error si el build falló. null si no ha fallado.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Contenido del Dockerfile generado a partir del template.</summary>
    public string? GeneratedDockerfile { get; set; }

    /// <summary>Ruta relativa al archivo .csproj detectado o proporcionado.</summary>
    public string? CsprojPath { get; set; }

    /// <summary>Nombre del assembly de salida (el .dll principal).</summary>
    public string? AssemblyName { get; set; }

    /// <summary>Fecha y hora UTC de creación del build.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Fecha y hora UTC de inicio del procesamiento.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Fecha y hora UTC de finalización (éxito o fallo).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Tamaño de la imagen construida en bytes (si se completó).</summary>
    public long? ImageSizeBytes { get; set; }

    /// <summary>Número de veces que se ha reintentado este build.</summary>
    public int RetryCount { get; set; }

    /// <summary>URL completa de la imagen en el registry. Ejemplo: repos.daviviendahn.dvhn/davivienda-banco/ms23:sapp-dev</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Configuración original del request serializada como JSON para soportar retry.</summary>
    public string? OriginalRequestJson { get; set; }

    /// <summary>Plataforma objetivo del build. Default: linux/amd64</summary>
    public string Platform { get; set; } = "linux/amd64";

    /// <summary>URL del registry donde se publicó la imagen.</summary>
    public string RegistryUrl { get; set; } = "repos.daviviendahn.dvhn";

    /// <summary>Owner/organización del registry.</summary>
    public string RegistryOwner { get; set; } = "davivienda-banco";

    /// <summary>Argumentos adicionales de build pasados a Buildah.</summary>
    public Dictionary<string, string>? BuildArgs { get; set; }

    /// <summary>Labels metadata de la imagen.</summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>Si es true, no usa cache de layers.</summary>
    public bool NoCache { get; set; }

    /// <summary>Si es true, siempre descarga la imagen base más reciente.</summary>
    public bool Pull { get; set; }

    /// <summary>Si es true, salida mínima del build.</summary>
    public bool Quiet { get; set; }

    /// <summary>Tipo de red durante el build.</summary>
    public NetworkMode Network { get; set; } = NetworkMode.Bridge;

    /// <summary>Nivel de detalle del progreso.</summary>
    public ProgressMode Progress { get; set; } = ProgressMode.Auto;
}
