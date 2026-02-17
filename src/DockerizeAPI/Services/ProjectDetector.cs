using System.Xml.Linq;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio encargado de detectar el proyecto .NET principal en un repositorio clonado.
/// Busca archivos .csproj, determina cual es el proyecto ejecutable principal,
/// extrae el AssemblyName, y construye las rutas necesarias para el template del Dockerfile.
/// </summary>
public class ProjectDetector
{
    private readonly ILogger<ProjectDetector> _logger;

    /// <summary>
    /// Inicializa el detector de proyectos.
    /// </summary>
    /// <param name="logger">Logger para registrar operaciones de deteccion.</param>
    public ProjectDetector(ILogger<ProjectDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Detecta el proyecto .NET principal en un directorio.
    /// Aplica reglas de prioridad para encontrar el .csproj correcto cuando hay multiples.
    /// </summary>
    /// <param name="workDirectory">Directorio raiz del repositorio clonado.</param>
    /// <returns>Informacion del proyecto detectado (ruta csproj y nombre del assembly).</returns>
    /// <exception cref="InvalidOperationException">Si no se encuentra ningun archivo .csproj.</exception>
    public ProjectInfo Detect(string workDirectory)
    {
        // Buscar todos los archivos .csproj en el repositorio
        var csprojFiles = Directory.GetFiles(workDirectory, "*.csproj", SearchOption.AllDirectories);

        if (csprojFiles.Length == 0)
            throw new InvalidOperationException("No se encontro ningun archivo .csproj en el repositorio.");

        _logger.LogInformation("Se encontraron {Count} archivo(s) .csproj en el repositorio", csprojFiles.Length);

        string selectedCsproj;

        if (csprojFiles.Length == 1)
        {
            // Si hay un solo .csproj, usarlo directamente
            selectedCsproj = csprojFiles[0];
        }
        else
        {
            // Si hay multiples, aplicar reglas de seleccion en orden de prioridad
            selectedCsproj = SelectBestCsproj(csprojFiles, workDirectory);
        }

        var relativePath = Path.GetRelativePath(workDirectory, selectedCsproj).Replace('\\', '/');
        var assemblyName = ExtractAssemblyName(selectedCsproj);

        _logger.LogInformation(
            "Proyecto detectado: csprojPath={CsprojPath}, assemblyName={AssemblyName}",
            relativePath, assemblyName);

        return new ProjectInfo
        {
            CsprojPath = relativePath,
            AssemblyName = assemblyName
        };
    }

    /// <summary>
    /// Selecciona el mejor .csproj cuando hay multiples en el repositorio.
    /// Reglas de prioridad:
    /// 1. El que tenga OutputType=Exe (es el ejecutable principal)
    /// 2. El que este en una carpeta con "Presentation" o "API" en el nombre
    /// 3. El primero encontrado como fallback
    /// </summary>
    /// <param name="csprojFiles">Lista de archivos .csproj encontrados.</param>
    /// <param name="workDirectory">Directorio raiz del repositorio.</param>
    /// <returns>Ruta completa al .csproj seleccionado.</returns>
    private string SelectBestCsproj(string[] csprojFiles, string workDirectory)
    {
        // Regla 1: Buscar el que tenga <OutputType>Exe</OutputType>
        foreach (var csproj in csprojFiles)
        {
            try
            {
                var doc = XDocument.Load(csproj);
                var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
                if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Seleccionado .csproj con OutputType=Exe: {Path}", csproj);
                    return csproj;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al parsear {CsprojPath}, continuando con el siguiente", csproj);
            }
        }

        // Regla 2: Buscar el que este en carpeta "Presentation" o "API"
        foreach (var csproj in csprojFiles)
        {
            var relativePath = Path.GetRelativePath(workDirectory, csproj);
            if (relativePath.Contains("Presentation", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("API", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Seleccionado .csproj en carpeta Presentation/API: {Path}", csproj);
                return csproj;
            }
        }

        // Regla 3: Usar el primero encontrado
        _logger.LogWarning("No se pudo determinar el .csproj principal, usando el primero: {Path}", csprojFiles[0]);
        return csprojFiles[0];
    }

    /// <summary>
    /// Extrae el AssemblyName del archivo .csproj.
    /// Busca el tag &lt;AssemblyName&gt; en el XML.
    /// Si no existe, usa el nombre del archivo .csproj sin extension como fallback.
    /// </summary>
    /// <param name="csprojPath">Ruta completa al archivo .csproj.</param>
    /// <returns>Nombre del assembly (sin extension .dll).</returns>
    private string ExtractAssemblyName(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            var assemblyName = doc.Descendants("AssemblyName").FirstOrDefault()?.Value;

            if (!string.IsNullOrWhiteSpace(assemblyName))
                return assemblyName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al extraer AssemblyName de {CsprojPath}", csprojPath);
        }

        // Fallback: usar el nombre del archivo sin extension
        return Path.GetFileNameWithoutExtension(csprojPath);
    }
}

/// <summary>
/// Informacion del proyecto .NET detectado en el repositorio.
/// Contiene la ruta al .csproj y el nombre del assembly para generar el Dockerfile.
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// Ruta relativa al archivo .csproj desde la raiz del repositorio.
    /// Ejemplo: "1.1-Presentation/Microservices.csproj"
    /// </summary>
    public string CsprojPath { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del assembly de salida (sin extension .dll).
    /// Ejemplo: "Microservices"
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;
}
