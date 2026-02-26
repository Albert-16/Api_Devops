using System.Text;
using DockerizeAPI.Configuration;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DockerizeAPI.Services;

/// <summary>
/// Wrapper sobre el CLI de Docker para gestión de containers.
/// Todos los comandos emiten logs en tiempo real vía el broadcaster SSE.
/// Soporta ejecución vía WSL en Windows (configuración UseWsl).
/// SEGURIDAD: Nunca loguea tokens ni contraseñas.
/// </summary>
public sealed class DockerRunService : IDockerRunService
{
    private readonly ProcessRunner _processRunner;
    private readonly IDeployLogBroadcaster _broadcaster;
    private readonly BuildSettings _buildSettings;
    private readonly ILogger<DockerRunService> _logger;

    /// <summary>Inicializa el servicio con el runner de procesos, broadcaster y logger.</summary>
    public DockerRunService(
        ProcessRunner processRunner,
        IDeployLogBroadcaster broadcaster,
        IOptions<BuildSettings> buildSettings,
        ILogger<DockerRunService> logger)
    {
        _processRunner = processRunner;
        _broadcaster = broadcaster;
        _buildSettings = buildSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> LoginAsync(
        string registryUrl,
        string username,
        string password,
        Guid deployId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Autenticando en registry: {registryUrl}",
            cancellationToken: cancellationToken);

        string arguments = $"login -u {username} -p {password} {registryUrl}";

        ProcessResult result = await RunDockerAsync(
            arguments,
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(deployId,
                $"Autenticación exitosa en registry: {registryUrl}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Login exitoso en registry: {Registry}", registryUrl);
            return true;
        }

        string errorMessage = result.StdErr.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            ? "Autenticación al registry fallida. Verifique el gitToken."
            : $"No se pudo conectar al registry: {registryUrl}";

        await _broadcaster.BroadcastLogAsync(deployId, $"Error: {errorMessage}", "error", CancellationToken.None);
        _logger.LogError("Error de login en registry {Registry}: {Error}", registryUrl, errorMessage);
        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> PullImageAsync(
        string imageName,
        Guid deployId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Descargando imagen: {imageName}",
            cancellationToken: cancellationToken);

        string arguments = $"pull {imageName}";

        ProcessResult result = await RunDockerAsync(
            arguments,
            onOutputReceived: async line =>
                await _broadcaster.BroadcastLogAsync(deployId, line, cancellationToken: CancellationToken.None),
            onErrorReceived: async line =>
                await _broadcaster.BroadcastLogAsync(deployId, line, "warning", CancellationToken.None),
            timeoutSeconds: 300,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(deployId,
                $"Imagen descargada exitosamente: {imageName}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Imagen descargada: {ImageName}", imageName);
            return true;
        }

        string errorMessage = ClassifyPullError(result.StdErr, imageName);
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Error al descargar imagen: {errorMessage}", "error",
            CancellationToken.None);
        _logger.LogError("Error al descargar {ImageName}: {Error}", imageName, errorMessage);
        return false;
    }

    /// <inheritdoc/>
    public async Task<string?> RunContainerAsync(
        DockerRunOptions options,
        Guid deployId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Iniciando container: {options.ContainerName} con imagen {options.ImageName}",
            cancellationToken: cancellationToken);

        string arguments = BuildDockerRunArguments(options);

        ProcessResult result = await RunDockerAsync(
            arguments,
            onErrorReceived: async line =>
                await _broadcaster.BroadcastLogAsync(deployId, line, "warning", CancellationToken.None),
            timeoutSeconds: 60,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            string containerId = result.StdOut.Trim();
            // Docker run -d retorna el container ID completo
            if (containerId.Length > 12)
                containerId = containerId[..12];

            await _broadcaster.BroadcastLogAsync(deployId,
                $"Container iniciado: {options.ContainerName} (ID: {containerId})",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Container iniciado: {ContainerName} (ID: {ContainerId})", options.ContainerName, containerId);
            return containerId;
        }

        string errorMessage = ClassifyRunError(result.StdErr, options.ContainerName);
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Error al iniciar container: {errorMessage}", "error",
            CancellationToken.None);
        _logger.LogError("Error al iniciar container {ContainerName}: {Error}", options.ContainerName, errorMessage);
        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> ContainerExistsAsync(string containerName, CancellationToken cancellationToken = default)
    {
        // docker ps -a --filter name=^/containerName$ --format "{{.Names}}"
        string arguments = $"ps -a --filter name=^/{containerName}$ --format \"{{{{.Names}}}}\"";

        ProcessResult result = await RunDockerAsync(
            arguments,
            timeoutSeconds: 10,
            cancellationToken: cancellationToken);

        return result.IsSuccess && !string.IsNullOrWhiteSpace(result.StdOut);
    }

    /// <inheritdoc/>
    public async Task<bool> StopContainerAsync(
        string containerName,
        Guid deployId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Deteniendo container: {containerName}",
            cancellationToken: cancellationToken);

        ProcessResult result = await RunDockerAsync(
            $"stop {containerName}",
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(deployId,
                $"Container detenido: {containerName}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Container detenido: {ContainerName}", containerName);
            return true;
        }

        await _broadcaster.BroadcastLogAsync(deployId,
            $"Error al detener container: {result.StdErr.Trim()}", "error",
            CancellationToken.None);
        _logger.LogError("Error al detener container {ContainerName}: {Error}", containerName, result.StdErr.Trim());
        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveContainerAsync(
        string containerName,
        Guid deployId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Eliminando container: {containerName}",
            cancellationToken: cancellationToken);

        ProcessResult result = await RunDockerAsync(
            $"rm {containerName}",
            timeoutSeconds: 10,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(deployId,
                $"Container eliminado: {containerName}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Container eliminado: {ContainerName}", containerName);
            return true;
        }

        await _broadcaster.BroadcastLogAsync(deployId,
            $"Error al eliminar container: {result.StdErr.Trim()}", "warning",
            CancellationToken.None);
        _logger.LogWarning("Error al eliminar container {ContainerName}: {Error}", containerName, result.StdErr.Trim());
        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> RestartContainerAsync(
        string containerName,
        Guid deployId,
        CancellationToken cancellationToken = default)
    {
        await _broadcaster.BroadcastLogAsync(deployId,
            $"Reiniciando container: {containerName}",
            cancellationToken: cancellationToken);

        ProcessResult result = await RunDockerAsync(
            $"restart {containerName}",
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            await _broadcaster.BroadcastLogAsync(deployId,
                $"Container reiniciado: {containerName}",
                cancellationToken: CancellationToken.None);
            _logger.LogInformation("Container reiniciado: {ContainerName}", containerName);
            return true;
        }

        await _broadcaster.BroadcastLogAsync(deployId,
            $"Error al reiniciar container: {result.StdErr.Trim()}", "error",
            CancellationToken.None);
        _logger.LogError("Error al reiniciar container {ContainerName}: {Error}", containerName, result.StdErr.Trim());
        return false;
    }

    /// <inheritdoc/>
    public async Task<string?> InspectContainerAsync(
        string containerName,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunDockerAsync(
            $"inspect {containerName}",
            timeoutSeconds: 10,
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            return result.StdOut.Trim();
        }

        _logger.LogWarning("Error al inspeccionar container {ContainerName}: {Error}", containerName, result.StdErr.Trim());
        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> IsContainerRunningAsync(
        string containerName,
        CancellationToken cancellationToken = default)
    {
        // docker inspect --format "{{.State.Running}}" containerName
        string arguments = $"inspect --format \"{{{{.State.Running}}}}\" {containerName}";

        ProcessResult result = await RunDockerAsync(
            arguments,
            timeoutSeconds: 10,
            cancellationToken: cancellationToken);

        return result.IsSuccess && result.StdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<string> GetContainerLogsAsync(
        string containerName,
        int? tail = null,
        CancellationToken cancellationToken = default)
    {
        string tailArg = tail.HasValue ? $" --tail {tail.Value}" : "";
        string arguments = $"logs{tailArg} {containerName}";

        ProcessResult result = await RunDockerAsync(
            arguments,
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);

        // Docker logs escribe a stdout y stderr, combinar ambos
        var combined = new StringBuilder();
        if (!string.IsNullOrEmpty(result.StdOut))
            combined.Append(result.StdOut);
        if (!string.IsNullOrEmpty(result.StdErr))
            combined.Append(result.StdErr);

        return combined.ToString();
    }

    /// <summary>
    /// Ejecuta un comando docker, opcionalmente vía WSL si UseWsl está habilitado.
    /// </summary>
    private Task<ProcessResult> RunDockerAsync(
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
            string wslWorkDir = workingDirectory is not null
                ? $"--cd \"{ToWslPath(workingDirectory)}\" "
                : "";
            finalArguments = $"{wslWorkDir}docker {arguments}";
        }
        else
        {
            fileName = "docker";
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

        if (windowsPath.StartsWith('/'))
            return windowsPath;

        string absolutePath = Path.GetFullPath(windowsPath);
        string normalized = absolutePath.Replace('\\', '/');
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            char driveLetter = char.ToLowerInvariant(normalized[0]);
            return $"/mnt/{driveLetter}{normalized[2..]}";
        }

        return normalized;
    }

    /// <summary>
    /// Construye la cadena de argumentos para docker run con todos los flags configurados.
    /// </summary>
    public static string BuildDockerRunArguments(DockerRunOptions options)
    {
        var args = new StringBuilder();
        args.Append("run");

        // Detached mode
        if (options.Detached)
            args.Append(" -d");

        // Interactive mode
        if (options.Interactive)
            args.Append(" -i");

        // Container name
        args.Append($" --name {options.ContainerName}");

        // Ports
        foreach (string port in options.Ports)
        {
            args.Append($" -p {port}");
        }

        // Volumes
        foreach (string volume in options.Volumes)
        {
            args.Append($" -v {volume}");
        }

        // Environment variables
        foreach (KeyValuePair<string, string> env in options.EnvironmentVariables)
        {
            args.Append($" -e \"{env.Key}={env.Value}\"");
        }

        // Restart policy
        if (!string.IsNullOrEmpty(options.RestartPolicy) && options.RestartPolicy != "no")
        {
            args.Append($" --restart {options.RestartPolicy}");
        }

        // Network
        if (!string.IsNullOrEmpty(options.Network))
        {
            args.Append($" --network {options.Network}");
        }

        // Image (must be last)
        args.Append($" {options.ImageName}");

        return args.ToString();
    }

    /// <summary>
    /// Convierte un RestartPolicy enum a su equivalente docker string.
    /// </summary>
    public static string RestartPolicyToString(RestartPolicy policy, int maxRetries = 0)
    {
        return policy switch
        {
            RestartPolicy.No => "no",
            RestartPolicy.Always => "always",
            RestartPolicy.UnlessStopped => "unless-stopped",
            RestartPolicy.OnFailure when maxRetries > 0 => $"on-failure:{maxRetries}",
            RestartPolicy.OnFailure => "on-failure",
            _ => "no"
        };
    }

    /// <summary>Clasifica errores del pull para dar mensajes descriptivos.</summary>
    private static string ClassifyPullError(string stderr, string imageName)
    {
        if (stderr.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("manifest unknown", StringComparison.OrdinalIgnoreCase))
            return $"Imagen no encontrada en el registry: {imageName}";

        if (stderr.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("denied", StringComparison.OrdinalIgnoreCase))
            return $"Permisos insuficientes para descargar imagen: {imageName}";

        if (stderr.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "Timeout al descargar imagen del registry.";

        return stderr.Trim();
    }

    /// <summary>Clasifica errores del run para dar mensajes descriptivos.</summary>
    private static string ClassifyRunError(string stderr, string containerName)
    {
        if (stderr.Contains("already in use", StringComparison.OrdinalIgnoreCase))
            return $"Ya existe un container con el nombre: {containerName}";

        if (stderr.Contains("port is already allocated", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
            return "Puerto ya está en uso por otro proceso o container.";

        if (stderr.Contains("No space left", StringComparison.OrdinalIgnoreCase))
            return "Espacio en disco insuficiente.";

        return stderr.Trim();
    }
}
