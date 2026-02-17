using System.Text.Json;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DockerizeAPI.Endpoints;

/// <summary>
/// Endpoints relacionados con la gestion de builds.
/// Incluye: crear build, consultar estado, listar, cancelar, reintentar, logs y preview.
/// </summary>
public static class BuildEndpoints
{
    /// <summary>
    /// Registra todos los endpoints de builds en la aplicacion.
    /// </summary>
    /// <param name="app">Instancia de WebApplication.</param>
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/builds")
            .WithTags("Builds")
            .WithOpenApi();

        // POST /api/builds - Iniciar nuevo build
        group.MapPost("/", CreateBuild)
            .WithName("CreateBuild")
            .WithSummary("Iniciar un nuevo proceso de build")
            .WithDescription(
                "Recibe la configuracion del build (repositorio, rama, opciones de imagen) " +
                "y lo encola para procesamiento asincrono. Retorna inmediatamente con un buildId.")
            .Produces<BuildResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests);

        // GET /api/builds/{buildId} - Consultar estado de un build
        group.MapGet("/{buildId}", GetBuild)
            .WithName("GetBuild")
            .WithSummary("Consultar el estado de un build")
            .WithDescription("Retorna el estado actual, timestamps y logs parciales de un build especifico.")
            .Produces<BuildResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // GET /api/builds - Historial de builds
        group.MapGet("/", GetBuilds)
            .WithName("GetBuilds")
            .WithSummary("Listar historial de builds")
            .WithDescription("Lista todos los builds con filtros opcionales por estado, rama y repositorio. Soporta paginacion.")
            .Produces<BuildListResponse>();

        // DELETE /api/builds/{buildId} - Cancelar build
        group.MapDelete("/{buildId}", CancelBuild)
            .WithName("CancelBuild")
            .WithSummary("Cancelar un build en progreso")
            .WithDescription("Cancela un build que este en estado Queued, Cloning, Building o Pushing.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // GET /api/builds/{buildId}/logs - Logs del build
        group.MapGet("/{buildId}/logs", GetBuildLogs)
            .WithName("GetBuildLogs")
            .WithSummary("Obtener logs completos de un build")
            .WithDescription(
                "Retorna los logs completos de un build. " +
                "Si se solicita con Accept: text/event-stream, envia logs en tiempo real via SSE.")
            .Produces<List<string>>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        // POST /api/builds/{buildId}/retry - Reintentar build fallido
        group.MapPost("/{buildId}/retry", RetryBuild)
            .WithName("RetryBuild")
            .WithSummary("Reintentar un build fallido")
            .WithDescription("Crea un nuevo build con la misma configuracion de un build que fallo previamente.")
            .Produces<BuildResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        // POST /api/builds/preview-dockerfile - Preview del Dockerfile
        group.MapPost("/preview-dockerfile", PreviewDockerfile)
            .WithName("PreviewDockerfile")
            .WithSummary("Generar preview del Dockerfile")
            .WithDescription(
                "Genera el Dockerfile que se usaria para un build con los parametros dados, " +
                "sin ejecutar ningun build. Util para verificar el Dockerfile antes de construir.")
            .Produces<PreviewDockerfileResponse>();
    }

    /// <summary>
    /// Crea un nuevo build y lo encola para procesamiento.
    /// </summary>
    private static async Task<IResult> CreateBuild(
        [FromBody] CreateBuildRequest request,
        BuildService buildService)
    {
        var response = await buildService.CreateBuildAsync(request);
        return Results.Accepted($"/api/builds/{response.BuildId}", response);
    }

    /// <summary>
    /// Obtiene el estado actual de un build.
    /// </summary>
    private static IResult GetBuild(string buildId, BuildService buildService)
    {
        var response = buildService.GetBuild(buildId);
        return response is not null
            ? Results.Ok(response)
            : Results.NotFound(new ErrorResponse { Message = $"Build '{buildId}' no encontrado." });
    }

    /// <summary>
    /// Lista builds con filtros y paginacion.
    /// </summary>
    private static IResult GetBuilds(
        BuildService buildService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] BuildStatus? status = null,
        [FromQuery] string? branch = null,
        [FromQuery] string? repositoryUrl = null)
    {
        var response = buildService.GetBuilds(page, pageSize, status, branch, repositoryUrl);
        return Results.Ok(response);
    }

    /// <summary>
    /// Cancela un build en progreso.
    /// </summary>
    private static IResult CancelBuild(string buildId, BuildService buildService)
    {
        var cancelled = buildService.CancelBuild(buildId);
        return cancelled
            ? Results.NoContent()
            : Results.NotFound(new ErrorResponse { Message = $"Build '{buildId}' no encontrado o ya finalizo." });
    }

    /// <summary>
    /// Obtiene los logs de un build. Soporta SSE para logs en tiempo real.
    /// Si el header Accept es "text/event-stream", envia logs via SSE.
    /// </summary>
    private static async Task GetBuildLogs(
        string buildId,
        HttpContext httpContext,
        BuildService buildService)
    {
        var buildInfo = buildService.GetBuildInfo(buildId);

        if (buildInfo is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await httpContext.Response.WriteAsJsonAsync(
                new ErrorResponse { Message = $"Build '{buildId}' no encontrado." });
            return;
        }

        // Si el cliente solicita SSE, enviar logs en tiempo real
        var acceptHeader = httpContext.Request.Headers.Accept.ToString();
        if (acceptHeader.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            await SendLogsAsSSE(httpContext, buildInfo, buildService);
            return;
        }

        // Modo normal: retornar todos los logs como JSON
        var logs = buildService.GetBuildLogs(buildId) ?? [];
        await httpContext.Response.WriteAsJsonAsync(logs);
    }

    /// <summary>
    /// Envia logs en tiempo real via Server-Sent Events.
    /// Mantiene la conexion abierta y envia nuevos logs conforme aparecen.
    /// Se cierra cuando el build finaliza (Completed, Failed o Cancelled).
    /// </summary>
    private static async Task SendLogsAsSSE(
        HttpContext httpContext,
        Models.BuildInfo buildInfo,
        BuildService buildService)
    {
        httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");

        var lastIndex = 0;
        var cancellationToken = httpContext.RequestAborted;

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentLogs = buildService.GetBuildLogs(buildInfo.BuildId) ?? [];

            // Enviar solo los logs nuevos desde el ultimo indice
            for (var i = lastIndex; i < currentLogs.Count; i++)
            {
                await httpContext.Response.WriteAsync($"data: {currentLogs[i]}\n\n", cancellationToken);
            }
            lastIndex = currentLogs.Count;

            await httpContext.Response.Body.FlushAsync(cancellationToken);

            // Si el build ya termino, enviar evento de cierre y salir
            if (buildInfo.Status is BuildStatus.Completed or BuildStatus.Failed or BuildStatus.Cancelled)
            {
                await httpContext.Response.WriteAsync(
                    $"event: done\ndata: {{\"status\":\"{buildInfo.Status}\"}}\n\n",
                    cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
                break;
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    /// <summary>
    /// Reintenta un build fallido.
    /// </summary>
    private static async Task<IResult> RetryBuild(string buildId, BuildService buildService)
    {
        var response = await buildService.RetryBuildAsync(buildId);

        if (response is null)
            return Results.NotFound(new ErrorResponse
            {
                Message = $"Build '{buildId}' no encontrado o no esta en estado Failed."
            });

        return Results.Accepted($"/api/builds/{response.BuildId}", response);
    }

    /// <summary>
    /// Genera una vista previa del Dockerfile sin ejecutar el build.
    /// </summary>
    private static IResult PreviewDockerfile(
        [FromBody] PreviewDockerfileRequest request,
        DockerfileGenerator generator)
    {
        var content = generator.Generate(
            request.IncludeOdbcDependencies,
            request.CsprojPath,
            request.AssemblyName);

        return Results.Ok(new PreviewDockerfileResponse
        {
            TemplateName = request.IncludeOdbcDependencies ? "odbc" : "alpine",
            DockerfileContent = content
        });
    }
}
