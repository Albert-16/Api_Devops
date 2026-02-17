using System.Diagnostics;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio encargado de clonar repositorios desde Gitea.
/// Usa el comando git clone con autenticacion por token.
/// </summary>
public class GitService
{
    private readonly ILogger<GitService> _logger;

    /// <summary>
    /// Inicializa el servicio de Git.
    /// </summary>
    /// <param name="logger">Logger para registrar operaciones de clonacion.</param>
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Clona un repositorio de Gitea en un directorio local.
    /// Usa --single-branch para clonar solo la rama especificada (mas rapido).
    /// </summary>
    /// <param name="repositoryUrl">URL del repositorio en Gitea.</param>
    /// <param name="branch">Rama a clonar.</param>
    /// <param name="gitToken">Token de acceso para autenticacion.</param>
    /// <param name="workDirectory">Directorio destino donde se clonara el repo.</param>
    /// <param name="onLog">Callback para capturar lineas de log en tiempo real.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <exception cref="InvalidOperationException">Si el clone falla.</exception>
    public async Task CloneAsync(
        string repositoryUrl,
        string branch,
        string gitToken,
        string workDirectory,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        // Insertar el token en la URL para autenticacion
        // Formato: https://token@host/org/repo.git
        var authenticatedUrl = InsertTokenInUrl(repositoryUrl, gitToken);

        var arguments = $"clone --branch {branch} --single-branch {authenticatedUrl} {workDirectory}";

        _logger.LogInformation("Clonando repositorio: {Url} rama {Branch} en {Dir}",
            repositoryUrl, branch, workDirectory);

        // Logear sin el token por seguridad
        onLog($"[GIT] Clonando {repositoryUrl} (rama: {branch})...");

        var exitCode = await RunProcessAsync("git", arguments, null, onLog, cancellationToken);

        if (exitCode != 0)
            throw new InvalidOperationException($"git clone fallo con codigo de salida {exitCode}");

        onLog("[GIT] Clonacion completada exitosamente.");
        _logger.LogInformation("Repositorio clonado exitosamente en {Dir}", workDirectory);
    }

    /// <summary>
    /// Inserta el token de autenticacion en la URL del repositorio.
    /// Transforma "https://host/org/repo" en "https://token@host/org/repo".
    /// </summary>
    /// <param name="url">URL original del repositorio.</param>
    /// <param name="token">Token de acceso a Gitea.</param>
    /// <returns>URL con token de autenticacion embebido.</returns>
    private static string InsertTokenInUrl(string url, string token)
    {
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url.Insert("https://".Length, $"{token}@");

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return url.Insert("http://".Length, $"{token}@");

        return url;
    }

    /// <summary>
    /// Ejecuta un proceso externo y captura su salida en tiempo real.
    /// </summary>
    /// <param name="fileName">Ejecutable a ejecutar.</param>
    /// <param name="arguments">Argumentos del proceso.</param>
    /// <param name="workingDirectory">Directorio de trabajo (opcional).</param>
    /// <param name="onLog">Callback para cada linea de salida.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>Codigo de salida del proceso.</returns>
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

        // Leer stdout y stderr en paralelo para evitar deadlocks
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
