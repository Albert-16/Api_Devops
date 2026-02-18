using DockerizeAPI.Extensions;
using Serilog;

// ─── Bootstrap Logger (antes de que la app se configure) ───
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando DockerizeAPI — Servicio de Dockerización Automatizada");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // ─── Serilog (reemplaza el logging por defecto) ───
    builder.Host.UseSerilog((context, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(context.Configuration));

    // ─── Registrar todos los servicios ───
    builder.Services.AddDockerizeServices(builder.Configuration);

    WebApplication app = builder.Build();

    // ─── Configurar pipeline de middleware y endpoints ───
    app.ConfigurePipeline();

    Log.Information("DockerizeAPI configurado. Escuchando en: {Urls}", string.Join(", ", app.Urls));

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Clase parcial para permitir WebApplicationFactory en tests de integración.</summary>
public partial class Program { }
