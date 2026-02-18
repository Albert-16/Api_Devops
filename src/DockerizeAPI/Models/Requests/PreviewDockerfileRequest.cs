using System.ComponentModel.DataAnnotations;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Request para generar un preview del Dockerfile sin ejecutar el build.
/// Permite verificar el Dockerfile antes de construir la imagen.
/// </summary>
public sealed record PreviewDockerfileRequest
{
    /// <summary>Si es true, genera el template Debian con ODBC. Si es false, genera Alpine.</summary>
    public bool IncludeOdbcDependencies { get; init; }

    /// <summary>Ruta relativa al archivo .csproj. Ejemplo: "1.1-Presentation/Microservices.csproj"</summary>
    [Required(ErrorMessage = "csprojPath es requerido para generar el preview")]
    public required string CsprojPath { get; init; }

    /// <summary>Nombre del assembly de salida. Si es null, se extrae del nombre del .csproj.</summary>
    public string? AssemblyName { get; init; }
}
