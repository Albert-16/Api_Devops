using System.Diagnostics;

namespace DockerizeAPI.Middleware;

/// <summary>
/// Middleware de logging estructurado para requests HTTP con Serilog.
/// Registra: método, ruta, status code y duración de cada request.
/// Excluye health checks para no contaminar los logs.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>Rutas excluidas del logging (health checks, swagger).</summary>
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health/live",
        "/health/ready",
        "/api/health",
        "/swagger",
        "/favicon.ico"
    };

    /// <summary>Inicializa el middleware.</summary>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invoca el siguiente middleware con logging de request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Excluir rutas de health check y swagger
        string path = context.Request.Path.Value ?? "/";
        if (ExcludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} respondió {StatusCode} en {Duration}ms",
                context.Request.Method,
                path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "HTTP {Method} {Path} falló en {Duration}ms",
                context.Request.Method,
                path,
                stopwatch.ElapsedMilliseconds);

            throw; // Re-lanzar para que GlobalExceptionMiddleware lo maneje
        }
    }
}
