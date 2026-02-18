namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta con el contenido de un template Dockerfile.
/// </summary>
public sealed record TemplateResponse
{
    /// <summary>Nombre del template: "alpine" u "odbc".</summary>
    public required string Name { get; init; }

    /// <summary>Contenido completo del template Dockerfile.</summary>
    public required string Content { get; init; }

    /// <summary>Indica si es un override (modificado por el usuario) o el template original embebido.</summary>
    public bool IsOverride { get; init; }

    /// <summary>Fecha de última modificación (solo para overrides).</summary>
    public DateTimeOffset? LastModifiedAt { get; init; }
}
