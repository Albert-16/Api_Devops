namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta que contiene el contenido de un template de Dockerfile.
/// </summary>
public class TemplateResponse
{
    /// <summary>
    /// Nombre del template ("alpine" u "odbc").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Contenido completo del template con placeholders sin reemplazar.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de la ultima modificacion del template.
    /// </summary>
    public DateTime LastModified { get; set; }
}
