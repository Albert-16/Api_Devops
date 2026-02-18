using System.Reflection;
using DockerizeAPI.BackgroundServices;
using DockerizeAPI.Configuration;
using DockerizeAPI.Data;
using DockerizeAPI.Services;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Extensions;

/// <summary>
/// Métodos de extensión para registrar todos los servicios de la aplicación en el contenedor de DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra todos los servicios, configuración, background services y Swagger.
    /// </summary>
    public static IServiceCollection AddDockerizeServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ─── Options Pattern ───
        services.Configure<RegistrySettings>(configuration.GetSection(RegistrySettings.SectionName));
        services.Configure<BuildSettings>(configuration.GetSection(BuildSettings.SectionName));
        services.Configure<OdbcPackagesSettings>(configuration.GetSection(OdbcPackagesSettings.SectionName));

        // ─── Data Store (Singleton — en memoria) ───
        services.AddSingleton<BuildStore>();

        // ─── Servicios Singleton ───
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<BuildChannel>();
        services.AddSingleton<IBuildLogBroadcaster, BuildLogBroadcaster>();

        // ─── Servicios Singleton ───
        // Registrados como Singleton porque son stateless y thread-safe,
        // y son consumidos por BuildProcessorService (HostedService/Singleton).
        services.AddSingleton<IBuildService, BuildService>();
        services.AddSingleton<ITemplateService, TemplateService>();
        services.AddSingleton<IDockerfileGenerator, DockerfileGenerator>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IBuildahService, BuildahService>();

        // ─── Background Service ───
        services.AddHostedService<BuildProcessorService>();

        // ─── Health Checks ───
        services.AddHealthChecks();

        // ─── Swagger / OpenAPI ───
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "DockerizeAPI",
                Version = "v1",
                Description = "API REST para construcción y publicación automatizada de imágenes Docker para microservicios .NET de Davivienda Honduras. " +
                              "Usa Buildah para construir imágenes y el Container Registry de Gitea para publicarlas."
            });

            // Incluir XML comments en Swagger
            string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
