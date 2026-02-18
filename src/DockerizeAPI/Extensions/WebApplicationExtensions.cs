using DockerizeAPI.Endpoints;
using DockerizeAPI.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace DockerizeAPI.Extensions;

/// <summary>
/// Métodos de extensión para configurar el pipeline de middleware y endpoints de la aplicación.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configura el pipeline completo: middleware, Swagger, health checks y endpoints.
    /// </summary>
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // ─── Middleware (el orden importa) ───
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();

        // ─── Swagger (solo en Development) ───
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "DockerizeAPI v1");
                options.RoutePrefix = "swagger";
            });
        }

        // ─── Health Checks ASP.NET Core ───
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false // Solo verifica que la app responde
        });

        app.MapHealthChecks("/health/ready");

        // ─── Endpoints ───
        app.MapBuildEndpoints();
        app.MapTemplateEndpoints();
        app.MapHealthEndpoints();

        return app;
    }
}
