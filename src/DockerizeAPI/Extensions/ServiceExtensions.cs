using DockerizeAPI.BackgroundServices;
using DockerizeAPI.Models.Configuration;
using DockerizeAPI.Services;

namespace DockerizeAPI.Extensions;

/// <summary>
/// Metodos de extension para registrar todos los servicios de la aplicacion
/// en el contenedor de inyeccion de dependencias.
/// Centraliza la configuracion de servicios para mantener Program.cs limpio.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Registra todos los servicios de DockerizeAPI en el contenedor de DI.
    /// Incluye: configuracion desde appsettings, servicios de negocio,
    /// Background Service, Swagger/OpenAPI, y CORS.
    /// </summary>
    /// <param name="services">Coleccion de servicios del host.</param>
    /// <param name="configuration">Configuracion de la aplicacion (appsettings.json + env vars).</param>
    /// <returns>La misma coleccion de servicios para encadenamiento.</returns>
    public static IServiceCollection AddDockerizeServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registrar configuracion tipada desde appsettings.json
        services.Configure<RegistrySettings>(configuration.GetSection(RegistrySettings.SectionName));
        services.Configure<BuildSettings>(configuration.GetSection(BuildSettings.SectionName));
        services.Configure<OdbcSettings>(configuration.GetSection(OdbcSettings.SectionName));

        // Registrar servicios de negocio como Singleton
        // (necesarios como Singleton porque BuildService mantiene estado en memoria)
        services.AddSingleton<TemplateService>();
        services.AddSingleton<DockerfileGenerator>();
        services.AddSingleton<ProjectDetector>();
        services.AddSingleton<GitService>();
        services.AddSingleton<BuildahService>();
        services.AddSingleton<BuildService>();

        // Registrar Background Service para procesamiento de builds
        services.AddHostedService<BuildProcessorService>();

        // Swagger / OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "DockerizeAPI",
                Version = "v1",
                Description = "API REST para construccion y publicacion automatizada de imagenes Docker " +
                              "en el Gitea Container Registry de Davivienda Honduras. " +
                              "Utiliza Buildah para construir imagenes sin necesidad de Docker daemon."
            });
        });

        // CORS permisivo (API interna sin autenticacion)
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }
}
