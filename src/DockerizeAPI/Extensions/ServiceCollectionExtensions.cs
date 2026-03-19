using System.Reflection;
using DockerizeAPI.BackgroundServices;
using DockerizeAPI.Configuration;
using DockerizeAPI.Data;
using DockerizeAPI.Sandbox;
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
        services.Configure<ServerSettings>(configuration.GetSection(ServerSettings.SectionName));
        services.Configure<RegistrySettings>(configuration.GetSection(RegistrySettings.SectionName));
        services.Configure<BuildSettings>(configuration.GetSection(BuildSettings.SectionName));
        services.Configure<OdbcPackagesSettings>(configuration.GetSection(OdbcPackagesSettings.SectionName));
        services.Configure<DeploySettings>(configuration.GetSection(DeploySettings.SectionName));

        // ─── Data Stores (Singleton — en memoria) ───
        services.AddSingleton<BuildStore>();
        services.AddSingleton<DeployStore>();

        // ─── Servicios Singleton (Build) ───
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<BuildChannel>();
        services.AddSingleton<IBuildLogBroadcaster, BuildLogBroadcaster>();

        // ─── Servicios Singleton (Build) ───
        // Registrados como Singleton porque son stateless y thread-safe,
        // y son consumidos por BuildProcessorService (HostedService/Singleton).
        services.AddSingleton<IBuildService, BuildService>();
        services.AddSingleton<ITemplateService, TemplateService>();
        services.AddSingleton<IDockerfileGenerator, DockerfileGenerator>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IDockerBuildService, DockerBuildService>();
        services.AddSingleton<ISharedFilesService, SharedFilesService>();

        // ─── Servicios Singleton (Deploy) ───
        services.AddSingleton<DeployChannel>();
        services.AddSingleton<IDeployLogBroadcaster, DeployLogBroadcaster>();
        services.AddSingleton<IDockerRunService, DockerRunService>();
        services.AddSingleton<IDeployService, DeployService>();

        // ─── Sandbox Simuladores ───
        services.AddSingleton<SandboxBuildSimulator>();
        services.AddSingleton<SandboxDeploySimulator>();

        // ─── Background Services ───
        services.AddHostedService<BuildProcessorService>();
        services.AddHostedService<DeployProcessorService>();

        // ─── Health Checks ───
        services.AddHealthChecks();

        // ─── Swagger / OpenAPI ───
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "Hefesto Forge",
                Version = "v1",
                Description = "Where containers are born in fire... by Innovaciones Transversales"
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
