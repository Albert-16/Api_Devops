using System.Text.RegularExpressions;
using System.Xml.Linq;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio para operaciones Git usando el CLI.
/// Clona repositorios de Gitea, detecta proyectos .csproj y extrae metadata.
/// SEGURIDAD: Sanitiza URLs con tokens antes de loguear.
/// </summary>
public sealed partial class GitService : IGitService
{
    private readonly ProcessRunner _processRunner;
    private readonly IBuildLogBroadcaster _broadcaster;
    private readonly ILogger<GitService> _logger;

    /// <summary>Inicializa el servicio con el runner de procesos, broadcaster y logger.</summary>
    public GitService(
        ProcessRunner processRunner,
        IBuildLogBroadcaster broadcaster,
        ILogger<GitService> logger)
    {
        _processRunner = processRunner;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> CloneAsync(
        string repositoryUrl,
        string branch,
        string workspacePath,
        Guid buildId,
        CancellationToken cancellationToken = default)
    {
        string sanitizedUrl = SanitizeUrl(repositoryUrl);
        await _broadcaster.BroadcastLogAsync(buildId,
            $"Clonando repositorio: {sanitizedUrl} rama {branch}",
            cancellationToken: cancellationToken);

        // Crear el directorio de workspace si no existe
        if (!Directory.Exists(workspacePath))
        {
            Directory.CreateDirectory(workspacePath);
        }

        string arguments = $"clone --branch {branch} --single-branch \"{repositoryUrl}\" \"{workspacePath}\"";

        ProcessResult result = await _processRunner.RunAsync(
            "git",
            arguments,
            onOutputReceived: async line =>
                await _broadcaster.BroadcastLogAsync(buildId, line, cancellationToken: CancellationToken.None),
            onErrorReceived: async line =>
                await _broadcaster.BroadcastLogAsync(buildId, line, "warning", CancellationToken.None),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            string errorMessage = ClassifyGitError(result.StdErr, repositoryUrl, branch);
            _logger.LogError("Error al clonar repositorio: {Error}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        await _broadcaster.BroadcastLogAsync(buildId,
            $"Repositorio clonado exitosamente: {sanitizedUrl} rama {branch}",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Repositorio clonado: {RepositoryUrl} rama {Branch} en {Path}",
            sanitizedUrl, branch, workspacePath);

        return workspacePath;
    }

    /// <inheritdoc/>
    public async Task<string?> GetCurrentCommitShaAsync(string repoPath, CancellationToken cancellationToken = default)
    {
        ProcessResult result = await _processRunner.RunAsync(
            "git",
            "rev-parse HEAD",
            workingDirectory: repoPath,
            cancellationToken: cancellationToken);

        return result.IsSuccess ? result.StdOut.Trim() : null;
    }

    /// <inheritdoc/>
    public Task<string> DetectCsprojAsync(string repoPath)
    {
        _logger.LogDebug("Buscando archivos .csproj en {RepoPath}", repoPath);

        // Buscar todos los .csproj recursivamente
        string[] csprojFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories);

        if (csprojFiles.Length == 0)
        {
            throw new InvalidOperationException(
                "No se encontró ningún archivo .csproj en el repositorio. " +
                "Verifique que el repositorio contiene un proyecto .NET.");
        }

        // Convertir a rutas relativas con separadores Unix
        List<string> relativePaths = csprojFiles
            .Select(f => Path.GetRelativePath(repoPath, f).Replace('\\', '/'))
            .ToList();

        // Si hay un solo .csproj, usarlo directamente
        if (relativePaths.Count == 1)
        {
            _logger.LogInformation("Proyecto detectado: {CsprojPath}", relativePaths[0]);
            return Task.FromResult(relativePaths[0]);
        }

        _logger.LogInformation(
            "Múltiples proyectos encontrados ({Count}). Aplicando reglas de selección.",
            relativePaths.Count);

        // Regla 1: Buscar el que tenga <OutputType>Exe</OutputType>
        foreach (string relativePath in relativePaths)
        {
            string fullPath = Path.Combine(repoPath, relativePath);
            try
            {
                string content = File.ReadAllText(fullPath);
                if (content.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Múltiples proyectos encontrados ({Count}). Seleccionado por OutputType=Exe: {Path}",
                        relativePaths.Count, relativePath);
                    return Task.FromResult(relativePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error leyendo {CsprojPath} para detección", relativePath);
            }
        }

        // Regla 2: Buscar el que esté en carpeta "Presentation" o "API"
        string? presentationProject = relativePaths.FirstOrDefault(p =>
            p.Contains("Presentation", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("API", StringComparison.OrdinalIgnoreCase));

        if (presentationProject is not null)
        {
            _logger.LogInformation(
                "Seleccionado por convención de carpeta: {Path}",
                presentationProject);
            return Task.FromResult(presentationProject);
        }

        // Regla 3: Usar el primero encontrado con warning
        _logger.LogWarning(
            "Múltiples proyectos encontrados sin criterio de selección claro. " +
            "Usando el primero: {Path}. Considere especificar csprojPath en el request.",
            relativePaths[0]);

        return Task.FromResult(relativePaths[0]);
    }

    /// <inheritdoc/>
    public string? ExtractAssemblyName(string csprojFullPath)
    {
        try
        {
            XDocument doc = XDocument.Load(csprojFullPath);
            string? assemblyName = doc.Descendants("AssemblyName").FirstOrDefault()?.Value;

            if (!string.IsNullOrWhiteSpace(assemblyName))
            {
                _logger.LogDebug("AssemblyName extraído del .csproj: {AssemblyName}", assemblyName);
                return assemblyName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el .csproj para extraer AssemblyName: {Path}", csprojFullPath);
        }

        _logger.LogWarning(
            "No se pudo extraer AssemblyName del .csproj, usando fallback: {Name}",
            Path.GetFileNameWithoutExtension(csprojFullPath));

        return null;
    }

    /// <summary>
    /// Clasifica errores de git clone para dar mensajes descriptivos al usuario.
    /// </summary>
    private static string ClassifyGitError(string stderr, string repositoryUrl, string branch)
    {
        string sanitizedUrl = SanitizeUrl(repositoryUrl);

        if (stderr.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("could not read Username", StringComparison.OrdinalIgnoreCase))
        {
            return "Autenticación fallida. Verifique el gitToken proporcionado.";
        }

        if (stderr.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return $"Repositorio no encontrado: {sanitizedUrl}";
        }

        if (stderr.Contains("Remote branch", StringComparison.OrdinalIgnoreCase) &&
            stderr.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return $"Rama '{branch}' no encontrada en el repositorio.";
        }

        if (stderr.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase))
        {
            return $"No se pudo conectar al servidor Gitea. Verifique la URL: {sanitizedUrl}";
        }

        if (stderr.Contains("No space left", StringComparison.OrdinalIgnoreCase))
        {
            return "Espacio en disco insuficiente para clonar el repositorio.";
        }

        return $"Error al clonar repositorio: {stderr.Trim()}";
    }

    /// <summary>
    /// Sanitiza una URL removiendo tokens embebidos para logging seguro.
    /// </summary>
    private static string SanitizeUrl(string url)
    {
        return TokenInUrlRegex().Replace(url, "$1[REDACTED]$3");
    }

    [GeneratedRegex(@"(https?://)([^@\s]+)(@)")]
    private static partial Regex TokenInUrlRegex();
}
