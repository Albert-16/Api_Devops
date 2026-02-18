using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace DockerizeAPI.Middleware;

/// <summary>
/// Middleware que captura excepciones no manejadas y retorna ProblemDetails (RFC 7807).
/// SEGURIDAD: Nunca expone stack traces ni detalles internos en producción.
/// Genera un correlationId único para cada error para facilitar troubleshooting.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>Inicializa el middleware.</summary>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>Invoca el siguiente middleware con manejo de excepciones.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente cerró la conexión, no es un error real
            _logger.LogDebug("Request cancelado por el cliente: {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499; // Client Closed Request
        }
        catch (ArgumentException ex)
        {
            await WriteErrorResponse(context, HttpStatusCode.BadRequest,
                "Error de validación", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontr", StringComparison.OrdinalIgnoreCase))
        {
            await WriteErrorResponse(context, HttpStatusCode.NotFound,
                "Recurso no encontrado", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no se puede", StringComparison.OrdinalIgnoreCase) ||
                                                     ex.Message.Contains("Solo se pueden", StringComparison.OrdinalIgnoreCase))
        {
            await WriteErrorResponse(context, HttpStatusCode.Conflict,
                "Conflicto", ex.Message);
        }
        catch (Exception ex)
        {
            string correlationId = Guid.NewGuid().ToString("N")[..12];
            _logger.LogError(ex,
                "Error no manejado. CorrelationId={CorrelationId}, Method={Method}, Path={Path}",
                correlationId, context.Request.Method, context.Request.Path);

            string detail = _environment.IsDevelopment()
                ? ex.Message
                : $"Ocurrió un error inesperado. Consulte los logs con correlationId: {correlationId}";

            await WriteErrorResponse(context, HttpStatusCode.InternalServerError,
                "Error interno", detail, correlationId);
        }
    }

    /// <summary>Escribe una respuesta ProblemDetails en el response.</summary>
    private static async Task WriteErrorResponse(
        HttpContext context,
        HttpStatusCode statusCode,
        string title,
        string detail,
        string? correlationId = null)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        if (correlationId is not null)
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
