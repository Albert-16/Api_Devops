namespace DockerizeAPI.Services;

/// <summary>
/// Servicio encargado de generar Dockerfiles a partir de templates.
/// Reemplaza los placeholders del template con los valores reales
/// del proyecto detectado en el repositorio clonado.
/// </summary>
public class DockerfileGenerator
{
    private readonly TemplateService _templateService;
    private readonly ILogger<DockerfileGenerator> _logger;

    /// <summary>
    /// Inicializa el generador de Dockerfiles.
    /// </summary>
    /// <param name="templateService">Servicio de templates para obtener los templates base.</param>
    /// <param name="logger">Logger para registrar operaciones de generacion.</param>
    public DockerfileGenerator(TemplateService templateService, ILogger<DockerfileGenerator> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Genera un Dockerfile completo reemplazando los placeholders del template
    /// con los valores del proyecto.
    /// </summary>
    /// <param name="includeOdbc">Si es true, usa el template ODBC/Debian; si es false, usa Alpine.</param>
    /// <param name="csprojPath">Ruta relativa al archivo .csproj. Ejemplo: "1.1-Presentation/Microservices.csproj"</param>
    /// <param name="assemblyName">Nombre del assembly de salida (sin .dll). Ejemplo: "Microservices"</param>
    /// <returns>Contenido del Dockerfile generado con todos los placeholders reemplazados.</returns>
    /// <exception cref="InvalidOperationException">Si el template no se encuentra.</exception>
    public string Generate(bool includeOdbc, string csprojPath, string assemblyName)
    {
        var templateName = includeOdbc ? "odbc" : "alpine";
        var template = _templateService.GetTemplate(templateName)
            ?? throw new InvalidOperationException($"Template '{templateName}' no encontrado.");

        // Extraer el directorio del csproj (ej: "1.1-Presentation/Microservices.csproj" -> "1.1-Presentation/")
        var csprojDir = GetCsprojDirectory(csprojPath);

        // Reemplazar todos los placeholders con los valores reales
        var dockerfile = template
            .Replace("{{csprojPath}}", csprojPath)
            .Replace("{{csprojDir}}", csprojDir)
            .Replace("{{assemblyName}}", assemblyName);

        _logger.LogInformation(
            "Dockerfile generado con template '{TemplateName}': csprojPath={CsprojPath}, assemblyName={AssemblyName}",
            templateName, csprojPath, assemblyName);

        return dockerfile;
    }

    /// <summary>
    /// Extrae el directorio del archivo .csproj a partir de su ruta relativa.
    /// Si el .csproj esta en la raiz, retorna una cadena vacia.
    /// </summary>
    /// <param name="csprojPath">Ruta relativa al .csproj.</param>
    /// <returns>Directorio del .csproj con trailing slash, o cadena vacia si esta en la raiz.</returns>
    private static string GetCsprojDirectory(string csprojPath)
    {
        var dir = Path.GetDirectoryName(csprojPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir))
            return string.Empty;
        return dir.EndsWith('/') ? dir : dir + "/";
    }
}
