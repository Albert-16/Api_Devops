using DockerizeAPI.Models.Responses;

namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el servicio de gestión de templates Dockerfile.
/// Soporta templates embebidos y overrides en disco.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Obtiene un template por nombre ("alpine" u "odbc"), incluyendo metadata.
    /// </summary>
    /// <param name="templateName">Nombre del template.</param>
    /// <returns>Template con su contenido y metadata.</returns>
    TemplateResponse GetTemplate(string templateName);

    /// <summary>
    /// Actualiza un template guardando un override en disco.
    /// </summary>
    /// <param name="templateName">Nombre del template.</param>
    /// <param name="content">Nuevo contenido del template.</param>
    /// <returns>Template actualizado.</returns>
    TemplateResponse UpdateTemplate(string templateName, string content);

    /// <summary>
    /// Obtiene el contenido raw del template apropiado para generar un Dockerfile.
    /// </summary>
    /// <param name="useOdbc">true para template Debian con ODBC, false para Alpine.</param>
    /// <returns>Contenido del template con placeholders sin reemplazar.</returns>
    string GetTemplateContent(bool useOdbc);
}
