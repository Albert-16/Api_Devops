namespace DockerizeAPI.Endpoints;

/// <summary>
/// Endpoints de health check para monitoreo y verificación del servicio.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Registra los endpoints de health check.</summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // GET /api/health — Health check general
        app.MapGet("/api/health", () => TypedResults.Ok(new
        {
            Status = "Healthy",
            Service = "DockerizeAPI",
            Timestamp = DateTimeOffset.UtcNow,
            Version = typeof(HealthEndpoints).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        }))
        .WithName("HealthCheck")
        .WithTags("Health")
        .WithDescription("Retorna el estado de salud de la API.");
    }
}
