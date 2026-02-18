using System.ComponentModel.DataAnnotations;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Request para actualizar el contenido de un template Dockerfile.
/// El nuevo contenido reemplaza el template actual (se guarda como override en disco).
/// </summary>
public sealed record UpdateTemplateRequest
{
    /// <summary>Nuevo contenido del template Dockerfile. Debe contener los placeholders necesarios.</summary>
    [Required(ErrorMessage = "content es requerido")]
    [MinLength(10, ErrorMessage = "El contenido del template es demasiado corto")]
    public required string Content { get; init; }
}
