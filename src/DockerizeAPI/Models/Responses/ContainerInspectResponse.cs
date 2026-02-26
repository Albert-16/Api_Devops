namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta con la información de docker inspect de un container.
/// </summary>
public sealed record ContainerInspectResponse
{
    /// <summary>Identificador del deploy asociado.</summary>
    public required Guid DeployId { get; init; }

    /// <summary>Nombre del container.</summary>
    public required string ContainerName { get; init; }

    /// <summary>JSON crudo de docker inspect.</summary>
    public required string InspectJson { get; init; }
}
