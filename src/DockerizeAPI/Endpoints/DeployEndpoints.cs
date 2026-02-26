using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DockerizeAPI.Endpoints;

/// <summary>
/// Endpoints para gestión de deploys de containers Docker.
/// Incluye: crear, consultar, listar, stop, restart, remove, logs SSE, inspect y rollback.
/// </summary>
public static class DeployEndpoints
{
    /// <summary>Registra todos los endpoints de deploys en /api/deploys.</summary>
    public static RouteGroupBuilder MapDeployEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/deploys")
            .WithTags("Deploys");

        // POST /api/deploys — Crear nuevo deploy (pull + run)
        group.MapPost("/", CreateDeployAsync)
            .WithName("CreateDeploy")
            .WithDescription("Inicia un nuevo deploy de container Docker. El deploy se procesa de forma asíncrona (login → pull → run).")
            .Produces<DeployResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /api/deploys — Listar deploys
        group.MapGet("/", GetDeploys)
            .WithName("GetDeploys")
            .WithDescription("Lista el historial de deploys con paginación y filtros opcionales.")
            .Produces<PagedResponse<DeployResponse>>();

        // GET /api/deploys/{deployId} — Detalle de deploy
        group.MapGet("/{deployId:guid}", GetDeployById)
            .WithName("GetDeployById")
            .WithDescription("Obtiene el estado detallado de un deploy incluyendo logs.")
            .Produces<DeployDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /api/deploys/{deployId}/stop — Detener container
        group.MapPost("/{deployId:guid}/stop", StopDeploy)
            .WithName("StopDeploy")
            .WithDescription("Detiene el container asociado a un deploy.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /api/deploys/{deployId}/restart — Reiniciar container
        group.MapPost("/{deployId:guid}/restart", RestartDeploy)
            .WithName("RestartDeploy")
            .WithDescription("Reinicia el container asociado a un deploy.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE /api/deploys/{deployId} — Stop + remove container
        group.MapDelete("/{deployId:guid}", RemoveDeploy)
            .WithName("RemoveDeploy")
            .WithDescription("Detiene y elimina el container asociado a un deploy.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/deploys/{deployId}/logs — Logs SSE/JSON
        group.MapGet("/{deployId:guid}/logs", StreamDeployLogs)
            .WithName("StreamDeployLogs")
            .WithDescription("Obtiene los logs del deploy. SSE si está en progreso, JSON si terminó.")
            .Produces<string>(contentType: "text/event-stream");

        // GET /api/deploys/{deployId}/inspect — Docker inspect
        group.MapGet("/{deployId:guid}/inspect", InspectDeploy)
            .WithName("InspectDeploy")
            .WithDescription("Ejecuta docker inspect sobre el container del deploy.")
            .Produces<ContainerInspectResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /api/deploys/{deployId}/rollback — Rollback a imagen anterior
        group.MapPost("/{deployId:guid}/rollback", RollbackDeploy)
            .WithName("RollbackDeploy")
            .WithDescription("Revierte a la imagen anterior del deploy. Crea un nuevo deploy con IsRollback=true.")
            .Produces<DeployResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    /// <summary>POST /api/deploys — Crear nuevo deploy.</summary>
    private static async Task<IResult> CreateDeployAsync(
        [FromBody] CreateDeployRequest request,
        IDeployService deployService,
        CancellationToken cancellationToken)
    {
        DeployResponse response = await deployService.CreateDeployAsync(request, cancellationToken);
        return TypedResults.Accepted($"/api/deploys/{response.DeployId}", response);
    }

    /// <summary>GET /api/deploys — Listado con paginación y filtros.</summary>
    private static IResult GetDeploys(
        IDeployService deployService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DeployStatus? status = null,
        [FromQuery] string? containerName = null,
        [FromQuery] string? imageName = null)
    {
        PagedResponse<DeployResponse> response = deployService.GetDeploys(page, pageSize, status, containerName, imageName);
        return TypedResults.Ok(response);
    }

    /// <summary>GET /api/deploys/{deployId} — Detalle del deploy.</summary>
    private static IResult GetDeployById(
        Guid deployId,
        IDeployService deployService)
    {
        DeployDetailResponse? response = deployService.GetDeployById(deployId);

        return response is null
            ? TypedResults.Problem(
                detail: $"Deploy {deployId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado")
            : TypedResults.Ok(response);
    }

    /// <summary>POST /api/deploys/{deployId}/stop — Detener container.</summary>
    private static async Task<IResult> StopDeploy(
        Guid deployId,
        IDeployService deployService,
        CancellationToken cancellationToken)
    {
        DeployDetailResponse? existing = deployService.GetDeployById(deployId);
        if (existing is null)
        {
            return TypedResults.Problem(
                detail: $"Deploy {deployId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado");
        }

        if (existing.Status != DeployStatus.Running)
        {
            return TypedResults.Problem(
                detail: $"Solo se pueden detener deploys en estado Running. Estado actual: {existing.Status}.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto");
        }

        bool stopped = await deployService.StopDeployAsync(deployId, cancellationToken);

        return stopped
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                detail: "No se pudo detener el container.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error interno");
    }

    /// <summary>POST /api/deploys/{deployId}/restart — Reiniciar container.</summary>
    private static async Task<IResult> RestartDeploy(
        Guid deployId,
        IDeployService deployService,
        CancellationToken cancellationToken)
    {
        DeployDetailResponse? existing = deployService.GetDeployById(deployId);
        if (existing is null)
        {
            return TypedResults.Problem(
                detail: $"Deploy {deployId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado");
        }

        if (existing.Status is not (DeployStatus.Running or DeployStatus.Stopped))
        {
            return TypedResults.Problem(
                detail: $"Solo se pueden reiniciar deploys en estado Running o Stopped. Estado actual: {existing.Status}.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto");
        }

        bool restarted = await deployService.RestartDeployAsync(deployId, cancellationToken);

        return restarted
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                detail: "No se pudo reiniciar el container.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error interno");
    }

    /// <summary>DELETE /api/deploys/{deployId} — Stop + remove container.</summary>
    private static async Task<IResult> RemoveDeploy(
        Guid deployId,
        IDeployService deployService,
        CancellationToken cancellationToken)
    {
        DeployDetailResponse? existing = deployService.GetDeployById(deployId);
        if (existing is null)
        {
            return TypedResults.Problem(
                detail: $"Deploy {deployId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado");
        }

        bool removed = await deployService.RemoveDeployAsync(deployId, cancellationToken);

        return removed
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                detail: "No se pudo eliminar el container.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error interno");
    }

    /// <summary>GET /api/deploys/{deployId}/logs — Stream de logs vía SSE.</summary>
    private static async Task StreamDeployLogs(
        Guid deployId,
        IDeployLogBroadcaster broadcaster,
        IDeployService deployService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        DeployDetailResponse? deploy = deployService.GetDeployById(deployId);
        if (deploy is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await httpContext.Response.WriteAsJsonAsync(new { error = $"Deploy {deployId} no encontrado." }, cancellationToken);
            return;
        }

        // Si el deploy ya terminó, retornar logs almacenados como JSON
        if (deploy.Status is DeployStatus.Running or DeployStatus.Stopped or DeployStatus.Failed or DeployStatus.Cancelled)
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(deploy.Logs ?? [], cancellationToken);
            return;
        }

        // SSE para deploys en progreso (Queued, LoggingIn, Pulling, Deploying)
        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        await foreach (string logLine in broadcaster.SubscribeAsync(deployId, cancellationToken))
        {
            await httpContext.Response.WriteAsync($"data: {logLine}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>GET /api/deploys/{deployId}/inspect — Docker inspect del container.</summary>
    private static async Task<IResult> InspectDeploy(
        Guid deployId,
        IDeployService deployService,
        CancellationToken cancellationToken)
    {
        DeployDetailResponse? existing = deployService.GetDeployById(deployId);
        if (existing is null)
        {
            return TypedResults.Problem(
                detail: $"Deploy {deployId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado");
        }

        ContainerInspectResponse? response = await deployService.InspectDeployAsync(deployId, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : TypedResults.Problem(
                detail: "No se pudo obtener la información del container. Verifique que el container existe.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Container no encontrado");
    }

    /// <summary>POST /api/deploys/{deployId}/rollback — Rollback a imagen anterior.</summary>
    private static async Task<IResult> RollbackDeploy(
        Guid deployId,
        IDeployService deployService,
        CancellationToken cancellationToken)
    {
        DeployDetailResponse? existing = deployService.GetDeployById(deployId);
        if (existing is null)
        {
            return TypedResults.Problem(
                detail: $"Deploy {deployId} no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado");
        }

        if (string.IsNullOrEmpty(existing.PreviousImageName))
        {
            return TypedResults.Problem(
                detail: "No hay versión anterior disponible para rollback.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto");
        }

        DeployResponse? response = await deployService.RollbackDeployAsync(deployId, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : TypedResults.Problem(
                detail: "No se pudo ejecutar el rollback.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error interno");
    }
}
