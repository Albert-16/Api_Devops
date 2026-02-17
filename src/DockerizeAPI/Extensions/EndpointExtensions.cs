using DockerizeAPI.Endpoints;

namespace DockerizeAPI.Extensions;

/// <summary>
/// Metodos de extension para registrar todos los endpoints de la API.
/// Centraliza el mapeo de rutas para mantener Program.cs limpio.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Registra todos los grupos de endpoints de DockerizeAPI.
    /// </summary>
    /// <param name="app">Instancia de WebApplication.</param>
    /// <returns>La misma instancia para encadenamiento.</returns>
    public static WebApplication MapDockerizeEndpoints(this WebApplication app)
    {
        BuildEndpoints.Map(app);
        TemplateEndpoints.Map(app);
        HealthEndpoints.Map(app);

        return app;
    }
}
