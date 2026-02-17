namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta con la vista previa del Dockerfile generado.
/// Permite verificar el Dockerfile antes de ejecutar un build.
/// </summary>
public class PreviewDockerfileResponse
{
    /// <summary>
    /// Nombre del template utilizado ("alpine" u "odbc").
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Contenido del Dockerfile con todos los placeholders reemplazados.
    /// </summary>
    public string DockerfileContent { get; set; } = string.Empty;
}
