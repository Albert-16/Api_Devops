using System.Diagnostics;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio encargado de interactuar con Buildah para construir y publicar imagenes.
/// Buildah es la herramienta de construccion de imagenes utilizada en Davivienda
/// (en lugar de Docker daemon).
/// </summary>
public class BuildahService
{
    private readonly ILogger<BuildahService> _logger;

    /// <summary>
    /// Inicializa el servicio de Buildah.
    /// </summary>
    /// <param name="logger">Logger para registrar operaciones de build.</param>
    public BuildahService(ILogger<BuildahService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Construye una imagen de contenedor usando Buildah.
    /// Ejecuta "buildah bud" con todos los parametros configurados.
    /// </summary>
    /// <param name="imageTag">Tag completo de la imagen (registry/org/name:tag).</param>
    /// <param name="workDirectory">Directorio con el codigo fuente y el Dockerfile.</param>
    /// <param name="dockerfilePath">Ruta al Dockerfile generado.</param>
    /// <param name="imageConfig">Configuracion de la imagen (plataforma, cache, args, etc.).</param>
    /// <param name="gitToken">Token de Gitea, se pasa como REPO_TOKEN si es build ODBC.</param>
    /// <param name="onLog">Callback para capturar lineas de log en tiempo real.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <exception cref="InvalidOperationException">Si el build falla.</exception>
    public async Task BuildAsync(
        string imageTag,
        string workDirectory,
        string dockerfilePath,
        ImageConfig imageConfig,
        string gitToken,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        var arguments = BuildArguments(imageTag, dockerfilePath, imageConfig, gitToken);

        _logger.LogInformation("Ejecutando buildah bud para imagen {ImageTag}", imageTag);
        onLog($"[BUILDAH] Construyendo imagen: {imageTag}");
        onLog($"[BUILDAH] Plataforma: {imageConfig.Platform}");
        onLog($"[BUILDAH] ODBC: {imageConfig.IncludeOdbcDependencies}");

        var exitCode = await RunProcessAsync("buildah", $"{arguments} {workDirectory}", null, onLog, cancellationToken);

        if (exitCode != 0)
            throw new InvalidOperationException($"buildah bud fallo con codigo de salida {exitCode}");

        onLog("[BUILDAH] Imagen construida exitosamente.");
        _logger.LogInformation("Imagen {ImageTag} construida exitosamente", imageTag);
    }

    /// <summary>
    /// Autentica en el Container Registry de Gitea usando Buildah.
    /// </summary>
    /// <param name="registryUrl">URL del registry (ej: repos.daviviendahn.dvhn).</param>
    /// <param name="username">Usuario para autenticacion (normalmente "token").</param>
    /// <param name="password">Token de acceso.</param>
    /// <param name="onLog">Callback para capturar lineas de log.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <exception cref="InvalidOperationException">Si el login falla.</exception>
    public async Task LoginAsync(
        string registryUrl,
        string username,
        string password,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        onLog($"[BUILDAH] Autenticandose en registry: {registryUrl}");

        var exitCode = await RunProcessAsync(
            "buildah",
            $"login -u {username} -p {password} {registryUrl}",
            null,
            onLog,
            cancellationToken);

        if (exitCode != 0)
            throw new InvalidOperationException($"buildah login fallo con codigo de salida {exitCode}");

        onLog("[BUILDAH] Autenticacion exitosa en el registry.");
    }

    /// <summary>
    /// Sube una imagen al Container Registry de Gitea.
    /// Despues de este paso, la imagen aparece en Gitea -> Paquetes -> Container.
    /// </summary>
    /// <param name="imageTag">Tag completo de la imagen a subir.</param>
    /// <param name="onLog">Callback para capturar lineas de log.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <exception cref="InvalidOperationException">Si el push falla.</exception>
    public async Task PushAsync(
        string imageTag,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        onLog($"[BUILDAH] Subiendo imagen al registry: {imageTag}");

        var exitCode = await RunProcessAsync(
            "buildah",
            $"push {imageTag}",
            null,
            onLog,
            cancellationToken);

        if (exitCode != 0)
            throw new InvalidOperationException($"buildah push fallo con codigo de salida {exitCode}");

        onLog("[BUILDAH] Imagen subida exitosamente al registry.");
    }

    /// <summary>
    /// Elimina una imagen local de Buildah para liberar espacio en disco.
    /// Se ejecuta como parte de la limpieza al finalizar un build.
    /// </summary>
    /// <param name="imageTag">Tag de la imagen a eliminar.</param>
    /// <param name="onLog">Callback para capturar lineas de log.</param>
    public async Task RemoveImageAsync(string imageTag, Action<string> onLog)
    {
        try
        {
            onLog($"[BUILDAH] Eliminando imagen local: {imageTag}");
            await RunProcessAsync("buildah", $"rmi {imageTag}", null, onLog, CancellationToken.None);
            onLog("[BUILDAH] Imagen local eliminada.");
        }
        catch (Exception ex)
        {
            // No fallar si no se puede eliminar la imagen local; solo logear la advertencia
            _logger.LogWarning(ex, "No se pudo eliminar la imagen local {ImageTag}", imageTag);
            onLog($"[BUILDAH] Advertencia: No se pudo eliminar imagen local: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica que Buildah este instalado y retorna su version.
    /// </summary>
    /// <returns>Version de Buildah o null si no esta disponible.</returns>
    public async Task<string?> GetVersionAsync()
    {
        try
        {
            var output = new List<string>();
            var exitCode = await RunProcessAsync(
                "buildah",
                "version",
                null,
                line => output.Add(line),
                CancellationToken.None);

            return exitCode == 0 ? string.Join(" ", output).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Construye la cadena de argumentos para el comando "buildah bud".
    /// Incluye todos los flags configurados (tag, platform, layers, build-args, labels, etc.).
    /// </summary>
    /// <param name="imageTag">Tag completo de la imagen.</param>
    /// <param name="dockerfilePath">Ruta al Dockerfile.</param>
    /// <param name="config">Configuracion de la imagen.</param>
    /// <param name="gitToken">Token de Gitea para REPO_TOKEN.</param>
    /// <returns>Cadena de argumentos completa para buildah bud.</returns>
    private static string BuildArguments(string imageTag, string dockerfilePath, ImageConfig config, string gitToken)
    {
        var args = new List<string>
        {
            "bud",
            $"--tag {imageTag}",
            $"--platform {config.Platform}",
            "--layers"
        };

        // REPO_TOKEN se agrega automaticamente para builds con ODBC
        if (config.IncludeOdbcDependencies)
            args.Add($"--build-arg REPO_TOKEN={gitToken}");

        if (config.NoCache)
            args.Add("--no-cache");

        if (config.Pull)
            args.Add("--pull");

        if (config.Quiet)
            args.Add("--quiet");

        // Tipo de red
        var network = config.Network switch
        {
            NetworkMode.Host => "host",
            NetworkMode.None => "none",
            _ => "bridge"
        };
        args.Add($"--network {network}");

        // Build arguments adicionales del usuario
        if (config.BuildArgs is not null)
        {
            foreach (var (key, value) in config.BuildArgs)
                args.Add($"--build-arg {key}={value}");
        }

        // Labels/metadata
        if (config.Labels is not null)
        {
            foreach (var (key, value) in config.Labels)
                args.Add($"--label {key}={value}");
        }

        // Secrets
        if (config.Secrets is not null)
        {
            foreach (var secret in config.Secrets)
                args.Add($"--secret id={secret.Id},src={secret.Value}");
        }

        args.Add($"-f {dockerfilePath}");

        return string.Join(" ", args);
    }

    /// <summary>
    /// Ejecuta un proceso externo y captura su salida en tiempo real.
    /// </summary>
    private static async Task<int> RunProcessAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty
        };

        process.Start();

        var stdoutTask = ReadStreamAsync(process.StandardOutput, onLog, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, onLog, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode;
    }

    /// <summary>
    /// Lee un stream de texto linea por linea y ejecuta el callback para cada linea.
    /// </summary>
    private static async Task ReadStreamAsync(
        StreamReader reader,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is not null)
                onLog(line);
        }
    }
}
