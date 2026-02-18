using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DockerizeAPI.Endpoints;

/// <summary>
/// Endpoints para gestión de builds de imágenes Docker.
/// Incluye: crear, consultar, listar, cancelar, reintentar, logs SSE y preview de Dockerfile.
/// </summary>
public static class BuildEndpoints
{
    /// <summary>Registra todos los endpoints de builds en /api/builds.</summary>
    public static RouteGroupBuilder MapBuildEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/builds")
            .WithTags("Builds");

        // POST /api/builds — Iniciar nuevo build
        group.MapPost("/", CreateBuildAsync)
            .WithName("CreateBuild")
            .WithDescription("Inicia un nuevo build de imagen Docker. El build se procesa de forma asíncrona.")
            .Produces<BuildResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /api/builds/{buildId} — Consultar estado de un build
        group.MapGet("/{buildId:guid}", GetBuildById)
            .WithName("GetBuildById")
            .WithDescription("Obtiene el estado detallado de un build incluyendo logs parciales.")
            .Produces<BuildDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/builds — Historial de builds
        group.MapGet("/", GetBuilds)
            .WithName("GetBuilds")
            .WithDescription("Lista el historial de builds con paginación y filtros opcionales.")
            .Produces<PagedResponse<BuildResponse>>();

        // DELETE /api/builds/{buildId} — Cancelar build en progreso
        group.MapDelete("/{buildId:guid}", CancelBuild)
            .WithName("CancelBuild")
            .WithDescription("Cancela un build en progreso o encolado.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /api/builds/{buildId}/logs — Logs completos con SSE
        group.MapGet("/{buildId:guid}/logs", StreamBuildLogs)
            .WithName("StreamBuildLogs")
            .WithDescription("Obtiene los logs del build. Soporta SSE para logs en tiempo real.")
            .Produces<string>(contentType: "text/event-stream");

        // POST /api/builds/{buildId}/retry — Reintentar build fallido
        group.MapPost("/{buildId:guid}/retry", RetryBuildAsync)
            .WithName("RetryBuild")
            .WithDescription("Reintenta un build que falló usando la configuración original.")
            .Produces<BuildResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /api/builds/preview-dockerfile — Preview del Dockerfile
        group.MapPost("/preview-dockerfile", PreviewDockerfile)
            .WithName("PreviewDockerfile")
            .WithDescription("Genera el Dockerfile que se usaría para un build sin ejecutarlo.")
            .Produces<DockerfilePreviewResponse>()
            .ProducesValidationProblem();

        return group;
    }

    /// <summary>POST /api/builds — Iniciar nuevo build.</summary>
    private static async Task<IResult> CreateBuildAsync(
        [FromBody] CreateBuildRequest request,
        IBuildService buildService,
        CancellationToken cancellationToken)
    {
        BuildResponse response = await buildService.CreateBuildAsync(request, cancellationToken);
        return TypedResults.Accepted($"/api/builds/{response.BuildId}", response);
    }

    /// <summary>GET /api/builds/{buildId} — Consultar estado detallado.</summary>
    private static IResult GetBuildById(
        Guid buildId,
        IBuildService buildService)
    {
        BuildDetailResponse? response = buildService.GetBuildById(buildId);

        return response is null
            ? TypedResults.Problem(
                detail: $"Build {buildId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado")
            : TypedResults.Ok(response);
    }

    /// <summary>GET /api/builds — Historial con paginación y filtros.</summary>
    private static IResult GetBuilds(
        IBuildService buildService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] BuildStatus? status = null,
        [FromQuery] string? branch = null,
        [FromQuery] string? repositoryUrl = null)
    {
        PagedResponse<BuildResponse> response = buildService.GetBuilds(page, pageSize, status, branch, repositoryUrl);
        return TypedResults.Ok(response);
    }

    /// <summary>DELETE /api/builds/{buildId} — Cancelar build.</summary>
    private static IResult CancelBuild(
        Guid buildId,
        IBuildService buildService)
    {
        BuildDetailResponse? existing = buildService.GetBuildById(buildId);
        if (existing is null)
        {
            return TypedResults.Problem(
                detail: $"Build {buildId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado");
        }

        bool cancelled = buildService.CancelBuild(buildId);

        return cancelled
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                detail: $"No se puede cancelar un build en estado {existing.Status}.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto");
    }

    /// <summary>GET /api/builds/{buildId}/logs — Stream de logs vía SSE.</summary>
    private static async Task StreamBuildLogs(
        Guid buildId,
        IBuildLogBroadcaster broadcaster,
        IBuildService buildService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Verificar que el build existe
        BuildDetailResponse? build = buildService.GetBuildById(buildId);
        if (build is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await httpContext.Response.WriteAsJsonAsync(new { error = $"Build {buildId} no encontrado." }, cancellationToken);
            return;
        }

        // Si el build ya terminó, retornar los logs almacenados como JSON
        if (build.Status is BuildStatus.Completed or BuildStatus.Failed or BuildStatus.Cancelled)
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(build.Logs ?? [], cancellationToken);
            return;
        }

        // SSE para builds en progreso
        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        await foreach (string logLine in broadcaster.SubscribeAsync(buildId, cancellationToken))
        {
            await httpContext.Response.WriteAsync($"data: {logLine}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>POST /api/builds/{buildId}/retry — Reintentar build fallido.</summary>
    private static async Task<IResult> RetryBuildAsync(
        Guid buildId,
        IBuildService buildService,
        CancellationToken cancellationToken)
    {
        BuildDetailResponse? existing = buildService.GetBuildById(buildId);
        if (existing is null)
        {
            return TypedResults.Problem(
                detail: $"Build {buildId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado");
        }

        if (existing.Status != BuildStatus.Failed)
        {
            return TypedResults.Problem(
                detail: "Solo se pueden reintentar builds en estado Failed.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto");
        }

        BuildResponse? response = await buildService.RetryBuildAsync(buildId, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : TypedResults.Problem(
                detail: "No se pudo reintentar el build.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error interno");
    }

    /// <summary>POST /api/builds/preview-dockerfile — Preview del Dockerfile.</summary>
    private static IResult PreviewDockerfile(
        [FromBody] PreviewDockerfileRequest request,
        IDockerfileGenerator generator)
    {
        DockerfilePreviewResponse response = generator.Preview(request);
        return TypedResults.Ok(response);
    }
}
