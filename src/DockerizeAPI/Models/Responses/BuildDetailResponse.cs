using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta detallada de un build. Incluye toda la información del build,
/// logs parciales, Dockerfile generado y timestamps de cada etapa.
/// </summary>
public sealed record BuildDetailResponse
{
    /// <summary>Identificador único del build.</summary>
    public required Guid BuildId { get; init; }

    /// <summary>Estado actual del build.</summary>
    public required BuildStatus Status { get; init; }

    /// <summary>URL del repositorio.</summary>
    public required string RepositoryUrl { get; init; }

    /// <summary>Rama del repositorio.</summary>
    public required string Branch { get; init; }

    /// <summary>SHA del commit construido (null si aún no se ha clonado).</summary>
    public string? CommitSha { get; init; }

    /// <summary>Nombre de la imagen (sin tag).</summary>
    public required string ImageName { get; init; }

    /// <summary>Tag de la imagen.</summary>
    public required string ImageTag { get; init; }

    /// <summary>Si es true, usa template Debian con ODBC.</summary>
    public bool IncludeOdbcDependencies { get; init; }

    /// <summary>Mensaje de error si el build falló.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Contenido del Dockerfile generado a partir del template.</summary>
    public string? GeneratedDockerfile { get; init; }

    /// <summary>Ruta al .csproj detectado o proporcionado.</summary>
    public string? CsprojPath { get; init; }

    /// <summary>Nombre del assembly de salida.</summary>
    public string? AssemblyName { get; init; }

    /// <summary>Fecha y hora UTC de creación.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Fecha y hora UTC de inicio del procesamiento.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Fecha y hora UTC de finalización.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Tamaño de la imagen en bytes.</summary>
    public long? ImageSizeBytes { get; init; }

    /// <summary>Número de reintentos realizados.</summary>
    public int RetryCount { get; init; }

    /// <summary>URL completa de la imagen en el registry.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Últimas líneas de log del build.</summary>
    public IReadOnlyList<BuildLogEntry>? Logs { get; init; }
}

/// <summary>
/// Entrada individual de log de un build.
/// </summary>
public sealed record BuildLogEntry
{
    /// <summary>Contenido del mensaje.</summary>
    public required string Message { get; init; }

    /// <summary>Nivel: info, warning, error.</summary>
    public required string Level { get; init; }

    /// <summary>Fecha y hora UTC del log.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
