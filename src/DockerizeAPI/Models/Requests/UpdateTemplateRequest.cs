using System.ComponentModel.DataAnnotations;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Request para actualizar el contenido de un template de Dockerfile.
/// Permite modificar los templates Alpine u ODBC en tiempo de ejecucion.
/// </summary>
public class UpdateTemplateRequest
{
    /// <summary>
    /// Contenido completo del template de Dockerfile.
    /// Puede incluir los placeholders {{csprojPath}}, {{csprojDir}} y {{assemblyName}}.
    /// </summary>
    [Required(ErrorMessage = "El contenido del template es requerido.")]
    [MinLength(10, ErrorMessage = "El template debe tener al menos 10 caracteres.")]
    public string Content { get; set; } = string.Empty;
}
