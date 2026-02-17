using System.Net;
using System.Text.Json;
using DockerizeAPI.Models.Responses;

namespace DockerizeAPI.Middleware;

/// <summary>
/// Middleware global de manejo de excepciones.
/// Captura todas las excepciones no manejadas y retorna respuestas
/// consistentes en formato JSON con codigos HTTP apropiados.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    /// <summary>
    /// Opciones de serializacion JSON para las respuestas de error.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Inicializa el middleware de excepciones.
    /// </summary>
    /// <param name="next">Siguiente middleware en la pipeline.</param>
    /// <param name="logger">Logger para registrar errores.</param>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el middleware. Si el siguiente middleware lanza una excepcion,
    /// la captura y genera una respuesta de error apropiada.
    /// </summary>
    /// <param name="context">Contexto HTTP de la solicitud.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Maneja una excepcion convirtiendo en una respuesta HTTP adecuada.
    /// InvalidOperationException -> 400 Bad Request
    /// Otras excepciones -> 500 Internal Server Error
    /// </summary>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Excepcion no manejada: {Message}", exception.Message);

        var (statusCode, message) = exception switch
        {
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            OperationCanceledException => (HttpStatusCode.RequestTimeout, "La operacion fue cancelada o excedio el tiempo limite."),
            _ => (HttpStatusCode.InternalServerError, "Ocurrio un error interno. Consulte los logs para mas detalles.")
        };

        var response = new ErrorResponse
        {
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
