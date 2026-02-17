using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services;
using System.Text.Json;

namespace DockerizeAPI.Middleware;

/// <summary>
/// Middleware de rate limiting basico para el endpoint de creacion de builds.
/// Rechaza solicitudes POST /api/builds cuando se alcanza el limite
/// de builds simultaneos configurado.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Opciones de serializacion JSON para las respuestas de error.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Inicializa el middleware de rate limiting.
    /// </summary>
    /// <param name="next">Siguiente middleware en la pipeline.</param>
    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Evalua si la solicitud excede el limite de builds simultaneos.
    /// Solo aplica a solicitudes POST al endpoint /api/builds.
    /// </summary>
    /// <param name="context">Contexto HTTP de la solicitud.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Solo aplicar rate limiting a POST /api/builds
        if (context.Request.Method == "POST" &&
            context.Request.Path.StartsWithSegments("/api/builds", StringComparison.OrdinalIgnoreCase))
        {
            var buildService = context.RequestServices.GetRequiredService<BuildService>();

            if (buildService.ActiveBuilds >= buildService.MaxConcurrentBuilds)
            {
                var response = new ErrorResponse
                {
                    Message = $"Se alcanzo el limite de {buildService.MaxConcurrentBuilds} builds simultaneos. Intente de nuevo mas tarde.",
                    Timestamp = DateTime.UtcNow
                };

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Append("Retry-After", "30");
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
                return;
            }
        }

        await _next(context);
    }
}
