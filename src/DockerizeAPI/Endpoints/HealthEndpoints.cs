using System.Reflection;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services;

namespace DockerizeAPI.Endpoints;

/// <summary>
/// Endpoint de health check para monitoreo de la API.
/// Verifica que la API esta activa, que Buildah esta disponible,
/// y reporta el estado de los builds activos.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Registra el endpoint de health check en la aplicacion.
    /// </summary>
    /// <param name="app">Instancia de WebApplication.</param>
    public static void Map(WebApplication app)
    {
        // GET /api/health - Health check
        app.MapGet("/api/health", GetHealth)
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Health check de la API")
            .WithDescription(
                "Retorna el estado de salud de la API incluyendo disponibilidad de Buildah, " +
                "builds activos y version de la aplicacion.")
            .WithOpenApi()
            .Produces<HealthResponse>();
    }

    /// <summary>
    /// Verifica el estado de salud de la API.
    /// Comprueba si Buildah esta instalado y retorna metricas basicas.
    /// </summary>
    private static async Task<IResult> GetHealth(
        BuildahService buildahService,
        BuildService buildService)
    {
        var buildahVersion = await buildahService.GetVersionAsync();

        var response = new HealthResponse
        {
            Status = buildahVersion is not null ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            BuildahAvailable = buildahVersion is not null,
            BuildahVersion = buildahVersion,
            ActiveBuilds = buildService.ActiveBuilds,
            MaxConcurrentBuilds = buildService.MaxConcurrentBuilds
        };

        return Results.Ok(response);
    }
}
