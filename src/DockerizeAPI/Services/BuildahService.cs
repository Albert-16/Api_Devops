using System.Text;
using DockerizeAPI.Configuration;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DockerizeAPI.Services;

/// <summary>
/// Wrapper sobre el CLI de Buildah para construir y publicar imágenes OCI.
/// No requiere Docker daemon. Usa Buildah 1.41.8.
/// Todos los comandos emiten logs en tiempo real vía el broadcaster SSE.
/// Soporta ejecución vía WSL en Windows (configuración UseWsl).
/// SEGURIDAD: Nunca loguea tokens ni contraseñas.
/// </summary>
public sealed class BuildahService : IBuildahService
{
    private readonly ProcessRunner _processRunner;
    private readonly IBuildLogBroadcaster _broadcaster;
    private readonly BuildSettings _buildSettings;
    private readonly ILogger<BuildahService> _logger;

    /// <summary>Inicializa el servicio con el runner de procesos, broadcaster y logger.</summary>
    public BuildahService(
        ProcessRunner processRunner,
        IBuildLogBroadcaster broadcaster,
        IOptions<BuildSettings> buildSettings,
        ILogger<BuildahService> logger)
    {
        _processRunner = processRunner;
        _broadcaster = broadcaster;
        _buildSettings = buildSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> BuildImageAsync(
        string contextPath,
        string dockerfilePath,
        string fullImageTag,
        Guid buildId,
        BuildahBuildOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(buildId,
            $"Iniciando construcción de imagen: {fullImageTag}",
            cancellationToken: cancellationToken);

        // Convertir paths a WSL si es necesario
        string effectiveDockerfilePath = _buildSettings.UseWsl ? ToWslPath(dockerfilePath) : dockerfilePath;
        string effectiveContextPath = _buildSettings.UseWsl ? ToWslPath(contextPath) : contextPath;

        string arguments = BuildBudArguments(effectiveDockerfilePath, fullImageTag, effectiveContextPath, options);

        ProcessResult result = await RunBuildahAsync(
            arguments,
            workingDirectory: contextPath,
            onOutputReceived: async line =>
                await _broadcaster.BroadcastLogAsync(buildId, line, cancellationToken: CancellationToken.None),
            onErrorReceived: async line =>
                await _broadcaster.BroadcastLogAsync(buildId, line, "warning", CancellationToken.None),
            timeoutSeconds: 600,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(buildId,
                $"Imagen construida exitosamente: {fullImageTag}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Imagen construida exitosamente: {ImageTag}", fullImageTag);
            return true;
        }

        string errorMessage = ClassifyBuildError(result.StdErr);
        await _broadcaster.BroadcastLogAsync(buildId,
            $"Error al construir imagen: {errorMessage}", "error",
            CancellationToken.None);
        _logger.LogError("Error al construir imagen {ImageTag}: {Error}", fullImageTag, errorMessage);
        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> LoginAsync(
        string registryUrl,
        string username,
        string password,
        Guid buildId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(buildId,
            $"Autenticando en registry: {registryUrl}",
            cancellationToken: cancellationToken);

        // SEGURIDAD: El password se pasa como argumento pero ProcessRunner lo sanitiza en logs
        string arguments = $"login -u {username} -p {password} {registryUrl}";

        ProcessResult result = await RunBuildahAsync(
            arguments,
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(buildId,
                $"Autenticación exitosa en registry: {registryUrl}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Login exitoso en registry: {Registry}", registryUrl);
            return true;
        }

        string errorMessage = result.StdErr.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            ? "Autenticación al registry fallida. Verifique el gitToken."
            : $"No se pudo conectar al registry: {registryUrl}";

        await _broadcaster.BroadcastLogAsync(buildId, $"Error: {errorMessage}", "error", CancellationToken.None);
        _logger.LogError("Error de login en registry {Registry}: {Error}", registryUrl, errorMessage);
        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> PushImageAsync(
        string fullImageTag,
        Guid buildId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(buildId,
            $"Publicando imagen: {fullImageTag}",
            cancellationToken: cancellationToken);

        string arguments = $"push {fullImageTag}";

        ProcessResult result = await RunBuildahAsync(
            arguments,
            onOutputReceived: async line =>
                await _broadcaster.BroadcastLogAsync(buildId, line, cancellationToken: CancellationToken.None),
            onErrorReceived: async line =>
                await _broadcaster.BroadcastLogAsync(buildId, line, "warning", CancellationToken.None),
            timeoutSeconds: 300,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(buildId,
                $"Imagen publicada exitosamente: {fullImageTag}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Imagen publicada: {ImageTag}", fullImageTag);
            return true;
        }

        string errorMessage = ClassifyPushError(result.StdErr, fullImageTag);
        await _broadcaster.BroadcastLogAsync(buildId,
            $"Error al publicar imagen: {errorMessage}", "error",
            CancellationToken.None);
        _logger.LogError("Error al publicar {ImageTag}: {Error}", fullImageTag, errorMessage);
        return false;
    }

    /// <inheritdoc/>
    public async Task CleanupImageAsync(string fullImageTag, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Limpiando imagen local: {ImageTag}", fullImageTag);

        ProcessResult result = await RunBuildahAsync(
            $"rmi {fullImageTag}",
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogDebug("Imagen local eliminada: {ImageTag}", fullImageTag);
        }
        else
        {
            // No falla si la imagen no existe, solo loguea warning
            _logger.LogWarning("No se pudo eliminar imagen local {ImageTag}: {Error}", fullImageTag, result.StdErr.Trim());
        }
    }

    /// <summary>
    /// Ejecuta un comando buildah, opcionalmente vía WSL si UseWsl está habilitado.
    /// </summary>
    private Task<ProcessResult> RunBuildahAsync(
        string arguments,
        string? workingDirectory = null,
        Func<string, Task>? onOutputReceived = null,
        Func<string, Task>? onErrorReceived = null,
        int timeoutSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        string fileName;
        string finalArguments;

        if (_buildSettings.UseWsl)
        {
            fileName = "wsl";
            // Convertir working directory de Windows a WSL path si se proporciona
            string wslWorkDir = workingDirectory is not null
                ? $"--cd \"{ToWslPath(workingDirectory)}\" "
                : "";
            finalArguments = $"{wslWorkDir}buildah {arguments}";
        }
        else
        {
            fileName = "buildah";
            finalArguments = arguments;
        }

        return _processRunner.RunAsync(
            fileName,
            finalArguments,
            workingDirectory: _buildSettings.UseWsl ? null : workingDirectory,
            onOutputReceived: onOutputReceived,
            onErrorReceived: onErrorReceived,
            timeoutSeconds: timeoutSeconds,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Convierte una ruta de Windows (D:\path\to) a ruta WSL (/mnt/d/path/to).
    /// </summary>
    private static string ToWslPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath))
            return windowsPath;

        // Si ya es una ruta Unix, retornar tal cual
        if (windowsPath.StartsWith('/'))
            return windowsPath;

        // Resolver rutas relativas a absolutas antes de convertir
        string absolutePath = Path.GetFullPath(windowsPath);

        // D:\Proyectos\foo → /mnt/d/Proyectos/foo
        string normalized = absolutePath.Replace('\\', '/');
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            char driveLetter = char.ToLowerInvariant(normalized[0]);
            return $"/mnt/{driveLetter}{normalized[2..]}";
        }

        return normalized;
    }

    /// <summary>
    /// Construye la cadena de argumentos para buildah bud con todos los flags configurados.
    /// </summary>
    private static string BuildBudArguments(
        string dockerfilePath,
        string fullImageTag,
        string contextPath,
        BuildahBuildOptions? options)
    {
        var args = new StringBuilder();
        args.Append("bud");

        // Tag de la imagen
        args.Append($" --tag {fullImageTag}");

        // Plataforma
        string platform = options?.Platform ?? "linux/amd64";
        args.Append($" --platform {platform}");

        // Habilitar cache por capas
        args.Append(" --layers");

        // Opciones condicionales
        if (options is not null)
        {
            if (options.NoCache)
                args.Append(" --no-cache");

            if (options.Pull)
                args.Append(" --pull");

            if (options.Quiet)
                args.Append(" --quiet");

            if (!string.IsNullOrEmpty(options.Network))
                args.Append($" --network {options.Network}");

            // Build args
            if (options.BuildArgs is not null)
            {
                foreach (KeyValuePair<string, string> buildArg in options.BuildArgs)
                {
                    args.Append($" --build-arg {buildArg.Key}={buildArg.Value}");
                }
            }

            // Labels
            if (options.Labels is not null)
            {
                foreach (KeyValuePair<string, string> label in options.Labels)
                {
                    args.Append($" --label {label.Key}={label.Value}");
                }
            }
        }

        // Dockerfile path y contexto
        args.Append($" -f \"{dockerfilePath}\"");
        args.Append($" \"{contextPath}\"");

        return args.ToString();
    }

    /// <summary>
    /// Clasifica errores del build para dar mensajes descriptivos.
    /// </summary>
    private static string ClassifyBuildError(string stderr)
    {
        if (stderr.Contains("No space left", StringComparison.OrdinalIgnoreCase))
            return "Espacio en disco insuficiente durante la construcción de la imagen.";

        if (stderr.Contains("manifest unknown", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "No se pudo descargar la imagen base. Verifique conectividad al registry.";

        return stderr.Trim();
    }

    /// <summary>
    /// Clasifica errores del push para dar mensajes descriptivos.
    /// </summary>
    private static string ClassifyPushError(string stderr, string imageTag)
    {
        if (stderr.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("denied", StringComparison.OrdinalIgnoreCase))
            return $"Permisos insuficientes para publicar imagen: {imageTag}";

        if (stderr.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "Timeout al publicar imagen al registry.";

        return stderr.Trim();
    }
}
