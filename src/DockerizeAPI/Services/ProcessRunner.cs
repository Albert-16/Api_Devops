using System.Diagnostics;
using System.Text;

namespace DockerizeAPI.Services;

/// <summary>
/// Ejecuta procesos externos (git, buildah) de forma asíncrona con captura de stdout/stderr,
/// soporte de timeout y callback para streaming de logs en tiempo real.
/// SEGURIDAD: Nunca loguea tokens ni credenciales en los argumentos.
/// </summary>
public sealed class ProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    /// <summary>Inicializa el ProcessRunner con el logger.</summary>
    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta un proceso externo y retorna el resultado.
    /// Captura stdout/stderr de forma asíncrona y soporta timeout + cancelación.
    /// </summary>
    /// <param name="fileName">Ejecutable a invocar (ej: "git", "buildah").</param>
    /// <param name="arguments">Argumentos del proceso.</param>
    /// <param name="workingDirectory">Directorio de trabajo (null = directorio actual).</param>
    /// <param name="onOutputReceived">Callback invocado por cada línea de stdout (para streaming de logs).</param>
    /// <param name="onErrorReceived">Callback invocado por cada línea de stderr.</param>
    /// <param name="timeoutSeconds">Timeout máximo en segundos. Default: 600 (10 min).</param>
    /// <param name="cancellationToken">Token de cancelación externo.</param>
    /// <returns>Resultado con exit code, stdout completo y stderr completo.</returns>
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        Func<string, Task>? onOutputReceived = null,
        Func<string, Task>? onErrorReceived = null,
        int timeoutSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        string sanitizedArgs = SanitizeForLogging(arguments);
        _logger.LogDebug("Ejecutando proceso: {FileName} {Arguments}", fileName, sanitizedArgs);

        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty
        };

        using var process = new Process { StartInfo = startInfo };

        var outputCompleted = new TaskCompletionSource<bool>();
        var errorCompleted = new TaskCompletionSource<bool>();

        process.OutputDataReceived += async (_, e) =>
        {
            if (e.Data is null)
            {
                outputCompleted.TrySetResult(true);
                return;
            }

            stdOutBuilder.AppendLine(e.Data);

            if (onOutputReceived is not null)
            {
                try
                {
                    await onOutputReceived(e.Data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error en callback de stdout para {FileName}", fileName);
                }
            }
        };

        process.ErrorDataReceived += async (_, e) =>
        {
            if (e.Data is null)
            {
                errorCompleted.TrySetResult(true);
                return;
            }

            stdErrBuilder.AppendLine(e.Data);

            if (onErrorReceived is not null)
            {
                try
                {
                    await onErrorReceived(e.Data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error en callback de stderr para {FileName}", fileName);
                }
            }
        };

        try
        {
            if (!process.Start())
            {
                _logger.LogError("No se pudo iniciar el proceso: {FileName}", fileName);
                return new ProcessResult(-1, string.Empty, $"No se pudo iniciar el proceso: {fileName}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(linkedCts.Token);
            await Task.WhenAll(outputCompleted.Task, errorCompleted.Task)
                .WaitAsync(TimeSpan.FromSeconds(5));

            _logger.LogDebug(
                "Proceso finalizado: {FileName} ExitCode={ExitCode}",
                fileName,
                process.ExitCode);

            return new ProcessResult(process.ExitCode, stdOutBuilder.ToString(), stdErrBuilder.ToString());
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            KillProcess(process);
            string message = $"Timeout al ejecutar {fileName} (>{timeoutSeconds}s)";
            _logger.LogWarning(message);
            return new ProcessResult(-1, stdOutBuilder.ToString(), message);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            _logger.LogInformation("Proceso cancelado por el usuario: {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado ejecutando proceso: {FileName}", fileName);
            KillProcess(process);
            return new ProcessResult(-1, stdOutBuilder.ToString(), ex.Message);
        }
    }

    /// <summary>
    /// Mata el proceso y todo su árbol de procesos hijos.
    /// </summary>
    private void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogDebug("Proceso terminado forzosamente: PID={ProcessId}", process.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al intentar matar el proceso");
        }
    }

    /// <summary>
    /// Sanitiza argumentos para logging, ocultando tokens y credenciales.
    /// Reemplaza patrones comunes de tokens con [REDACTED].
    /// </summary>
    /// <param name="arguments">Argumentos originales.</param>
    /// <returns>Argumentos con credenciales ocultas.</returns>
    public static string SanitizeForLogging(string arguments)
    {
        if (string.IsNullOrEmpty(arguments))
            return arguments;

        string sanitized = arguments;

        // Ocultar tokens en URLs: https://token@host → https://[REDACTED]@host
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(https?://)([^@\s]+)(@)",
            "$1[REDACTED]$3");

        // Ocultar -p password en buildah login
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(-p\s+)\S+",
            "$1[REDACTED]");

        // Ocultar --build-arg REPO_TOKEN=valor
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(REPO_TOKEN=)\S+",
            "$1[REDACTED]");

        return sanitized;
    }
}

/// <summary>
/// Resultado de la ejecución de un proceso externo.
/// </summary>
/// <param name="ExitCode">Código de salida del proceso. 0 = éxito.</param>
/// <param name="StdOut">Contenido completo de stdout.</param>
/// <param name="StdErr">Contenido completo de stderr.</param>
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>Indica si el proceso terminó exitosamente (exit code 0).</summary>
    public bool IsSuccess => ExitCode == 0;
}
