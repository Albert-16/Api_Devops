using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Services;

/// <summary>
/// Genera Dockerfiles reemplazando los placeholders del template con valores reales.
/// Placeholders soportados: {{csprojPath}}, {{csprojDir}}, {{assemblyName}}.
/// </summary>
public sealed class DockerfileGenerator : IDockerfileGenerator
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<DockerfileGenerator> _logger;

    /// <summary>Inicializa el generador con el servicio de templates y logger.</summary>
    public DockerfileGenerator(ITemplateService templateService, ILogger<DockerfileGenerator> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Generate(bool useOdbc, string csprojPath, string? assemblyName = null)
    {
        string template = _templateService.GetTemplateContent(useOdbc);
        Dictionary<string, string> placeholders = BuildPlaceholders(csprojPath, assemblyName);

        string dockerfile = ReplacePlaceholders(template, placeholders);

        string templateType = useOdbc ? "odbc" : "alpine";
        _logger.LogInformation(
            "Dockerfile generado ({TemplateType}): csprojPath={CsprojPath}, assemblyName={AssemblyName}",
            templateType,
            placeholders["{{csprojPath}}"],
            placeholders["{{assemblyName}}"]);

        return dockerfile;
    }

    /// <inheritdoc/>
    public DockerfilePreviewResponse Preview(PreviewDockerfileRequest request)
    {
        string template = _templateService.GetTemplateContent(request.IncludeOdbcDependencies);
        Dictionary<string, string> placeholders = BuildPlaceholders(request.CsprojPath, request.AssemblyName);

        string dockerfile = ReplacePlaceholders(template, placeholders);

        string templateType = request.IncludeOdbcDependencies ? "odbc" : "alpine";

        return new DockerfilePreviewResponse
        {
            Content = dockerfile,
            TemplateType = templateType,
            Placeholders = placeholders
        };
    }

    /// <summary>
    /// Construye el diccionario de placeholders a partir de la ruta del .csproj.
    /// </summary>
    /// <param name="csprojPath">Ruta relativa al .csproj. Ejemplo: "1.1-Presentation/Microservices.csproj"</param>
    /// <param name="assemblyName">Nombre del assembly (override). Si null, se extrae del nombre del .csproj.</param>
    /// <returns>Diccionario de placeholders con sus valores.</returns>
    private Dictionary<string, string> BuildPlaceholders(string csprojPath, string? assemblyName)
    {
        // Normalizar separadores a formato Unix (/)
        string normalizedPath = csprojPath.Replace('\\', '/');

        // Extraer el directorio del .csproj
        string csprojDir = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? string.Empty;
        if (!string.IsNullOrEmpty(csprojDir) && !csprojDir.EndsWith('/'))
        {
            csprojDir += "/";
        }

        // Extraer el AssemblyName (fallback al nombre del archivo sin extensión)
        string resolvedAssemblyName = assemblyName
            ?? Path.GetFileNameWithoutExtension(normalizedPath);

        _logger.LogDebug(
            "Placeholders calculados: csprojPath={CsprojPath}, csprojDir={CsprojDir}, assemblyName={AssemblyName}",
            normalizedPath,
            csprojDir,
            resolvedAssemblyName);

        return new Dictionary<string, string>
        {
            ["{{csprojPath}}"] = normalizedPath,
            ["{{csprojDir}}"] = csprojDir,
            ["{{assemblyName}}"] = resolvedAssemblyName
        };
    }

    /// <summary>
    /// Reemplaza todos los placeholders en el template con sus valores.
    /// </summary>
    private static string ReplacePlaceholders(string template, Dictionary<string, string> placeholders)
    {
        string result = template;
        foreach (KeyValuePair<string, string> placeholder in placeholders)
        {
            result = result.Replace(placeholder.Key, placeholder.Value);
        }
        return result;
    }
}
