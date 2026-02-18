using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta resumida de un build. Usada para listados y como respuesta al crear un build.
/// </summary>
public sealed record BuildResponse
{
    /// <summary>Identificador único del build.</summary>
    public required Guid BuildId { get; init; }

    /// <summary>Estado actual del build.</summary>
    public required BuildStatus Status { get; init; }

    /// <summary>URL del repositorio que se está construyendo.</summary>
    public required string RepositoryUrl { get; init; }

    /// <summary>Rama del repositorio.</summary>
    public required string Branch { get; init; }

    /// <summary>Nombre de la imagen (sin tag).</summary>
    public required string ImageName { get; init; }

    /// <summary>Tag de la imagen.</summary>
    public required string ImageTag { get; init; }

    /// <summary>Si es true, usa template Debian con ODBC.</summary>
    public bool IncludeOdbcDependencies { get; init; }

    /// <summary>Fecha y hora UTC de creación.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Fecha y hora UTC de finalización (null si no ha terminado).</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>URL completa de la imagen en el registry (null si no se ha completado).</summary>
    public string? ImageUrl { get; init; }
}
