using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta detallada de un deploy. Incluye toda la información del deploy,
/// configuración del container, historial de rollback y logs.
/// </summary>
public sealed record DeployDetailResponse
{
    /// <summary>Identificador único del deploy.</summary>
    public required Guid DeployId { get; init; }

    /// <summary>Estado actual del deploy.</summary>
    public required DeployStatus Status { get; init; }

    /// <summary>Nombre completo de la imagen desplegada.</summary>
    public required string ImageName { get; init; }

    /// <summary>Nombre del container Docker.</summary>
    public required string ContainerName { get; init; }

    /// <summary>Mensaje de error si el deploy falló.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>ID del container Docker.</summary>
    public string? ContainerId { get; init; }

    // ─── Container Config ───

    /// <summary>Política de reinicio del container.</summary>
    public RestartPolicy RestartPolicy { get; init; }

    /// <summary>Red Docker del container.</summary>
    public string? Network { get; init; }

    /// <summary>Mapeo de puertos.</summary>
    public IReadOnlyList<string>? Ports { get; init; }

    /// <summary>Montajes de volúmenes.</summary>
    public IReadOnlyList<string>? Volumes { get; init; }

    /// <summary>Variables de entorno (sin valores sensibles).</summary>
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    // ─── Rollback ───

    /// <summary>Versión del deploy para este container.</summary>
    public int DeployVersion { get; init; }

    /// <summary>Imagen anterior disponible para rollback.</summary>
    public string? PreviousImageName { get; init; }

    /// <summary>true si este deploy fue un rollback.</summary>
    public bool IsRollback { get; init; }

    // ─── Timestamps ───

    /// <summary>Fecha y hora UTC de creación.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Fecha y hora UTC de inicio del procesamiento.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Fecha y hora UTC de finalización.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Número de reintentos realizados.</summary>
    public int RetryCount { get; init; }

    /// <summary>Si es true, este deploy fue ejecutado en modo sandbox (simulado).</summary>
    public bool IsSandbox { get; init; }

    /// <summary>Logs del deploy.</summary>
    public IReadOnlyList<DeployLogEntry>? Logs { get; init; }
}

/// <summary>
/// Entrada individual de log de un deploy.
/// </summary>
public sealed record DeployLogEntry
{
    /// <summary>Contenido del mensaje.</summary>
    public required string Message { get; init; }

    /// <summary>Nivel: info, warning, error.</summary>
    public required string Level { get; init; }

    /// <summary>Fecha y hora UTC del log.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
