using DockerizeAPI.Extensions;
using DockerizeAPI.Middleware;
using Serilog;

// =============================================================================
// DockerizeAPI - Servicio de Dockerizacion Automatizada para Davivienda Honduras
// =============================================================================
// API REST interna (sin autenticacion) para ambientes de desarrollo y UAT.
// Recibe la URL de un repositorio Gitea, clona el codigo fuente,
// genera un Dockerfile optimizado (Alpine o Debian+ODBC), construye la imagen
// con Buildah, y la publica en el Gitea Container Registry.
// =============================================================================

// Configurar Serilog para logging estructurado
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/dockerize-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Iniciando DockerizeAPI...");

    var builder = WebApplication.CreateBuilder(args);

    // Usar Serilog como proveedor de logging
    builder.Host.UseSerilog();

    // Registrar todos los servicios de DockerizeAPI
    builder.Services.AddDockerizeServices(builder.Configuration);

    var app = builder.Build();

    // Pipeline de middleware
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseCors();

    // Swagger UI disponible en todos los ambientes (API interna)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DockerizeAPI v1");
        options.RoutePrefix = "swagger";
    });

    // Registrar todos los endpoints
    app.MapDockerizeEndpoints();

    // Redirigir raiz a Swagger
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();

    Log.Information("DockerizeAPI iniciada exitosamente");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Error fatal al iniciar DockerizeAPI");
}
finally
{
    Log.CloseAndFlush();
}
