using System.Reflection;
using DockerizeAPI.Models.Responses;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio responsable de gestionar los templates de Dockerfile.
/// Carga los templates embebidos como recursos y permite actualizarlos en runtime.
/// Los templates se mantienen en memoria y se inicializan desde los recursos embebidos al arrancar.
/// </summary>
public class TemplateService
{
    private readonly Dictionary<string, TemplateInfo> _templates = new();
    private readonly ILogger<TemplateService> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Inicializa el servicio de templates cargando los recursos embebidos.
    /// </summary>
    /// <param name="logger">Logger para registrar operaciones sobre templates.</param>
    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger;
        LoadEmbeddedTemplates();
    }

    /// <summary>
    /// Obtiene el contenido de un template por nombre.
    /// </summary>
    /// <param name="templateName">Nombre del template: "alpine" u "odbc".</param>
    /// <returns>Contenido del template o null si no existe.</returns>
    public string? GetTemplate(string templateName)
    {
        lock (_lock)
        {
            return _templates.TryGetValue(templateName.ToLowerInvariant(), out var info)
                ? info.Content
                : null;
        }
    }

    /// <summary>
    /// Obtiene la respuesta completa del template incluyendo metadata.
    /// </summary>
    /// <param name="templateName">Nombre del template: "alpine" u "odbc".</param>
    /// <returns>Respuesta con nombre, contenido y fecha de modificacion, o null si no existe.</returns>
    public TemplateResponse? GetTemplateResponse(string templateName)
    {
        lock (_lock)
        {
            if (!_templates.TryGetValue(templateName.ToLowerInvariant(), out var info))
                return null;

            return new TemplateResponse
            {
                Name = templateName.ToLowerInvariant(),
                Content = info.Content,
                LastModified = info.LastModified
            };
        }
    }

    /// <summary>
    /// Actualiza el contenido de un template existente.
    /// </summary>
    /// <param name="templateName">Nombre del template a actualizar: "alpine" u "odbc".</param>
    /// <param name="content">Nuevo contenido del template.</param>
    /// <returns>True si se actualizo correctamente, false si el template no existe.</returns>
    public bool UpdateTemplate(string templateName, string content)
    {
        var key = templateName.ToLowerInvariant();
        lock (_lock)
        {
            if (!_templates.ContainsKey(key))
                return false;

            _templates[key] = new TemplateInfo
            {
                Content = content,
                LastModified = DateTime.UtcNow
            };
        }

        _logger.LogInformation("Template '{TemplateName}' actualizado correctamente", templateName);
        return true;
    }

    /// <summary>
    /// Carga los templates de Dockerfile desde los recursos embebidos del assembly.
    /// Se ejecuta una sola vez al inicializar el servicio.
    /// </summary>
    private void LoadEmbeddedTemplates()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames)
        {
            if (!resourceName.Contains("Templates.Dockerfile"))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) continue;

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            var templateName = resourceName switch
            {
                _ when resourceName.Contains("alpine") => "alpine",
                _ when resourceName.Contains("odbc") => "odbc",
                _ => null
            };

            if (templateName is null) continue;

            _templates[templateName] = new TemplateInfo
            {
                Content = content,
                LastModified = DateTime.UtcNow
            };

            _logger.LogInformation("Template '{TemplateName}' cargado desde recurso embebido", templateName);
        }
    }

    /// <summary>
    /// Informacion interna de un template incluyendo contenido y metadata.
    /// </summary>
    private class TemplateInfo
    {
        public string Content { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}
