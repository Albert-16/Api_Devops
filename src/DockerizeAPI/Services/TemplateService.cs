using System.Reflection;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio para gestión de templates Dockerfile.
/// Lee templates embebidos en el assembly como recursos y soporta overrides en disco.
/// Los overrides tienen prioridad sobre los templates embebidos.
/// </summary>
public sealed class TemplateService : ITemplateService
{
    private readonly ILogger<TemplateService> _logger;
    private readonly string _overridePath;

    /// <summary>Mapeo de nombre de template a nombre de recurso embebido.</summary>
    private static readonly Dictionary<string, string> TemplateResourceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alpine"] = "DockerizeAPI.Templates.Dockerfile.alpine.template",
        ["odbc"] = "DockerizeAPI.Templates.Dockerfile.odbc.template"
    };

    /// <summary>Inicializa el servicio con el logger y configura el directorio de overrides.</summary>
    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger;
        _overridePath = Path.Combine(AppContext.BaseDirectory, "template-overrides");
    }

    /// <inheritdoc/>
    public TemplateResponse GetTemplate(string templateName)
    {
        ValidateTemplateName(templateName);

        string overrideFilePath = GetOverrideFilePath(templateName);

        if (File.Exists(overrideFilePath))
        {
            _logger.LogDebug("Usando override de template '{TemplateName}' desde disco", templateName);
            string overrideContent = File.ReadAllText(overrideFilePath);
            var fileInfo = new FileInfo(overrideFilePath);

            return new TemplateResponse
            {
                Name = templateName.ToLowerInvariant(),
                Content = overrideContent,
                IsOverride = true,
                LastModifiedAt = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)
            };
        }

        _logger.LogDebug("Usando template embebido '{TemplateName}'", templateName);
        string embeddedContent = ReadEmbeddedTemplate(templateName);

        return new TemplateResponse
        {
            Name = templateName.ToLowerInvariant(),
            Content = embeddedContent,
            IsOverride = false,
            LastModifiedAt = null
        };
    }

    /// <inheritdoc/>
    public TemplateResponse UpdateTemplate(string templateName, string content)
    {
        ValidateTemplateName(templateName);

        string overrideFilePath = GetOverrideFilePath(templateName);
        string? directory = Path.GetDirectoryName(overrideFilePath);

        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(overrideFilePath, content);
        _logger.LogInformation("Template '{TemplateName}' actualizado como override en disco", templateName);

        return new TemplateResponse
        {
            Name = templateName.ToLowerInvariant(),
            Content = content,
            IsOverride = true,
            LastModifiedAt = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc/>
    public string GetTemplateContent(bool useOdbc)
    {
        string templateName = useOdbc ? "odbc" : "alpine";

        string overrideFilePath = GetOverrideFilePath(templateName);
        if (File.Exists(overrideFilePath))
        {
            _logger.LogDebug("Generando Dockerfile con override de template '{TemplateName}'", templateName);
            return File.ReadAllText(overrideFilePath);
        }

        return ReadEmbeddedTemplate(templateName);
    }

    /// <summary>
    /// Lee un template embebido del assembly.
    /// </summary>
    /// <param name="templateName">Nombre del template.</param>
    /// <returns>Contenido del template.</returns>
    /// <exception cref="InvalidOperationException">Si el recurso embebido no se encuentra.</exception>
    private string ReadEmbeddedTemplate(string templateName)
    {
        string resourceName = TemplateResourceMap[templateName.ToLowerInvariant()];
        Assembly assembly = Assembly.GetExecutingAssembly();

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _logger.LogError("Recurso embebido no encontrado: {ResourceName}", resourceName);
            throw new InvalidOperationException(
                $"Template '{templateName}' no encontrado. Verifique los recursos embebidos de la aplicación. " +
                $"Recurso esperado: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Valida que el nombre del template sea válido ("alpine" u "odbc").
    /// </summary>
    private static void ValidateTemplateName(string templateName)
    {
        if (!TemplateResourceMap.ContainsKey(templateName))
        {
            throw new ArgumentException(
                $"Template '{templateName}' no es válido. Use 'alpine' u 'odbc'.",
                nameof(templateName));
        }
    }

    /// <summary>
    /// Obtiene la ruta al archivo de override en disco.
    /// </summary>
    private string GetOverrideFilePath(string templateName)
    {
        return Path.Combine(_overridePath, $"Dockerfile.{templateName.ToLowerInvariant()}.template");
    }
}
