using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta resumida de un deploy. Usada para listados y como respuesta al crear un deploy.
/// </summary>
public sealed record DeployResponse
{
    /// <summary>Identificador único del deploy.</summary>
    public required Guid DeployId { get; init; }

    /// <summary>Estado actual del deploy.</summary>
    public required DeployStatus Status { get; init; }

    /// <summary>Nombre completo de la imagen desplegada.</summary>
    public required string ImageName { get; init; }

    /// <summary>Nombre del container Docker.</summary>
    public required string ContainerName { get; init; }

    /// <summary>Versión del deploy para este container.</summary>
    public int DeployVersion { get; init; }

    /// <summary>true si este deploy fue un rollback.</summary>
    public bool IsRollback { get; init; }

    /// <summary>Fecha y hora UTC de creación.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Fecha y hora UTC de finalización (null si no ha terminado).</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>ID del container Docker (null si aún no se ha creado).</summary>
    public string? ContainerId { get; init; }

    /// <summary>Si es true, este deploy fue ejecutado en modo sandbox (simulado).</summary>
    public bool IsSandbox { get; init; }
}
