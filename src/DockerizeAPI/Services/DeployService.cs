using System.Text.Json;
using DockerizeAPI.BackgroundServices;
using DockerizeAPI.Configuration;
using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio principal de gestión de deploys.
/// Crea registros en el DeployStore, los encola en el DeployChannel para procesamiento asíncrono,
/// y maneja consultas, gestión de containers y rollback.
/// </summary>
public sealed class DeployService : IDeployService
{
    private readonly DeployStore _store;
    private readonly DeployChannel _deployChannel;
    private readonly IDockerRunService _dockerRunService;
    private readonly RegistrySettings _registrySettings;
    private readonly ILogger<DeployService> _logger;

    /// <summary>Inicializa el servicio con sus dependencias.</summary>
    public DeployService(
        DeployStore store,
        DeployChannel deployChannel,
        IDockerRunService dockerRunService,
        IOptions<RegistrySettings> registrySettings,
        ILogger<DeployService> logger)
    {
        _store = store;
        _deployChannel = deployChannel;
        _dockerRunService = dockerRunService;
        _registrySettings = registrySettings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DeployResponse> CreateDeployAsync(CreateDeployRequest request, CancellationToken cancellationToken = default)
    {
        // Extraer registry URL de la imagen o usar default
        string registryUrl = ExtractRegistryUrl(request.ImageName) ?? _registrySettings.Url;

        var deployRecord = new DeployRecord
        {
            Id = Guid.NewGuid(),
            ImageName = request.ImageName,
            ContainerName = request.ContainerName,
            GitToken = request.GitToken,
            Status = DeployStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            Detached = request.Detached,
            Interactive = request.Interactive,
            RestartPolicy = request.RestartPolicy,
            OnFailureMaxRetries = request.OnFailureMaxRetries,
            Network = request.Network,
            Ports = request.Ports.ToList(),
            Volumes = request.Volumes.ToList(),
            EnvironmentVariables = new Dictionary<string, string>(request.EnvironmentVariables),
            RegistryUrl = registryUrl,
            OriginalRequestJson = JsonSerializer.Serialize(request),
            IsSandbox = request.Sandbox
        };

        _store.AddDeploy(deployRecord);

        // Encolar para procesamiento asíncrono
        var channelRequest = new DeployChannelRequest(
            deployRecord.Id,
            request.ImageName,
            request.ContainerName,
            request.GitToken,
            registryUrl,
            request.Sandbox,
            request.SimulateFailure,
            request.FailAtStep);

        await _deployChannel.Writer.WriteAsync(channelRequest, cancellationToken);

        _logger.LogInformation(
            "Deploy {DeployId} encolado{Sandbox}: {ImageName} → container {ContainerName}",
            deployRecord.Id, request.Sandbox ? " [SANDBOX]" : "", request.ImageName, request.ContainerName);

        return MapToResponse(deployRecord);
    }

    /// <inheritdoc/>
    public DeployDetailResponse? GetDeployById(Guid deployId)
    {
        DeployRecord? deploy = _store.GetDeploy(deployId);
        if (deploy is null)
            return null;

        IReadOnlyList<DeployLog> logs = _store.GetLogs(deployId);

        return new DeployDetailResponse
        {
            DeployId = deploy.Id,
            Status = deploy.Status,
            ImageName = deploy.ImageName,
            ContainerName = deploy.ContainerName,
            ErrorMessage = deploy.ErrorMessage,
            ContainerId = deploy.ContainerId,
            RestartPolicy = deploy.RestartPolicy,
            Network = deploy.Network,
            Ports = deploy.Ports,
            Volumes = deploy.Volumes,
            EnvironmentVariables = deploy.EnvironmentVariables,
            DeployVersion = deploy.DeployVersion,
            PreviousImageName = deploy.PreviousImageName,
            IsRollback = deploy.IsRollback,
            CreatedAt = deploy.CreatedAt,
            StartedAt = deploy.StartedAt,
            CompletedAt = deploy.CompletedAt,
            RetryCount = deploy.RetryCount,
            IsSandbox = deploy.IsSandbox,
            Logs = logs.Select(l => new DeployLogEntry
            {
                Message = l.Message,
                Level = l.Level,
                Timestamp = l.Timestamp
            }).ToList()
        };
    }

    /// <inheritdoc/>
    public PagedResponse<DeployResponse> GetDeploys(
        int page = 1,
        int pageSize = 20,
        DeployStatus? status = null,
        string? containerName = null,
        string? imageName = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        (IReadOnlyList<DeployRecord> items, int totalCount) = _store.GetDeploys(page, pageSize, status, containerName, imageName);

        return new PagedResponse<DeployResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc/>
    public async Task<bool> StopDeployAsync(Guid deployId, CancellationToken cancellationToken = default)
    {
        DeployRecord? deploy = _store.GetDeploy(deployId);
        if (deploy is null)
            return false;

        if (deploy.Status != DeployStatus.Running)
            return false;

        bool stopped = await _dockerRunService.StopContainerAsync(deploy.ContainerName, deployId, cancellationToken);
        if (stopped)
        {
            _store.UpdateDeploy(deployId, d =>
            {
                d.Status = DeployStatus.Stopped;
                d.CompletedAt = DateTimeOffset.UtcNow;
            });
            _logger.LogInformation("Deploy {DeployId} detenido: container {ContainerName}", deployId, deploy.ContainerName);
        }

        return stopped;
    }

    /// <inheritdoc/>
    public async Task<bool> RestartDeployAsync(Guid deployId, CancellationToken cancellationToken = default)
    {
        DeployRecord? deploy = _store.GetDeploy(deployId);
        if (deploy is null)
            return false;

        if (deploy.Status is not (DeployStatus.Running or DeployStatus.Stopped))
            return false;

        bool restarted = await _dockerRunService.RestartContainerAsync(deploy.ContainerName, deployId, cancellationToken);
        if (restarted)
        {
            _store.UpdateDeploy(deployId, d =>
            {
                d.Status = DeployStatus.Running;
                d.CompletedAt = null;
            });
            _logger.LogInformation("Deploy {DeployId} reiniciado: container {ContainerName}", deployId, deploy.ContainerName);
        }

        return restarted;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveDeployAsync(Guid deployId, CancellationToken cancellationToken = default)
    {
        DeployRecord? deploy = _store.GetDeploy(deployId);
        if (deploy is null)
            return false;

        // Detener si está corriendo
        if (deploy.Status == DeployStatus.Running)
        {
            await _dockerRunService.StopContainerAsync(deploy.ContainerName, deployId, cancellationToken);
        }

        bool removed = await _dockerRunService.RemoveContainerAsync(deploy.ContainerName, deployId, cancellationToken);
        if (removed)
        {
            _store.UpdateDeploy(deployId, d =>
            {
                d.Status = DeployStatus.Stopped;
                d.CompletedAt = DateTimeOffset.UtcNow;
            });
            _logger.LogInformation("Deploy {DeployId} eliminado: container {ContainerName}", deployId, deploy.ContainerName);
        }

        return removed;
    }

    /// <inheritdoc/>
    public async Task<ContainerInspectResponse?> InspectDeployAsync(Guid deployId, CancellationToken cancellationToken = default)
    {
        DeployRecord? deploy = _store.GetDeploy(deployId);
        if (deploy is null)
            return null;

        string? inspectJson = await _dockerRunService.InspectContainerAsync(deploy.ContainerName, cancellationToken);
        if (inspectJson is null)
            return null;

        return new ContainerInspectResponse
        {
            DeployId = deployId,
            ContainerName = deploy.ContainerName,
            InspectJson = inspectJson
        };
    }

    /// <inheritdoc/>
    public async Task<DeployResponse?> RollbackDeployAsync(Guid deployId, CancellationToken cancellationToken = default)
    {
        DeployRecord? deploy = _store.GetDeploy(deployId);
        if (deploy is null)
            return null;

        // Verificar que hay una imagen anterior para rollback
        if (string.IsNullOrEmpty(deploy.PreviousImageName))
            return null;

        // Deserializar config anterior si existe
        CreateDeployRequest? previousConfig = null;
        if (!string.IsNullOrEmpty(deploy.PreviousConfigJson))
        {
            try
            {
                previousConfig = JsonSerializer.Deserialize<CreateDeployRequest>(deploy.PreviousConfigJson);
            }
            catch
            {
                _logger.LogWarning("No se pudo deserializar PreviousConfigJson para deploy {DeployId}", deployId);
            }
        }

        // Crear nuevo deploy con la imagen anterior
        var rollbackRequest = new CreateDeployRequest
        {
            ImageName = deploy.PreviousImageName,
            GitToken = deploy.GitToken,
            ContainerName = deploy.ContainerName,
            Ports = previousConfig?.Ports ?? deploy.Ports.ToList(),
            Detached = previousConfig?.Detached ?? deploy.Detached,
            Interactive = previousConfig?.Interactive ?? deploy.Interactive,
            RestartPolicy = previousConfig?.RestartPolicy ?? deploy.RestartPolicy,
            OnFailureMaxRetries = previousConfig?.OnFailureMaxRetries ?? deploy.OnFailureMaxRetries,
            Volumes = previousConfig?.Volumes ?? deploy.Volumes.ToList(),
            Network = previousConfig?.Network ?? deploy.Network,
            EnvironmentVariables = previousConfig?.EnvironmentVariables ?? new Dictionary<string, string>(deploy.EnvironmentVariables)
        };

        DeployResponse response = await CreateDeployAsync(rollbackRequest, cancellationToken);

        // Marcar el nuevo deploy como rollback
        _store.UpdateDeploy(response.DeployId, d =>
        {
            d.IsRollback = true;
        });

        _logger.LogInformation(
            "Rollback iniciado para deploy {OriginalDeployId}: {PreviousImage} → nuevo deploy {NewDeployId}",
            deployId, deploy.PreviousImageName, response.DeployId);

        return response with { IsRollback = true };
    }

    /// <summary>Mapea un DeployRecord a DeployResponse.</summary>
    private static DeployResponse MapToResponse(DeployRecord record)
    {
        return new DeployResponse
        {
            DeployId = record.Id,
            Status = record.Status,
            ImageName = record.ImageName,
            ContainerName = record.ContainerName,
            DeployVersion = record.DeployVersion,
            IsRollback = record.IsRollback,
            CreatedAt = record.CreatedAt,
            CompletedAt = record.CompletedAt,
            ContainerId = record.ContainerId,
            IsSandbox = record.IsSandbox
        };
    }

    /// <summary>
    /// Extrae la URL del registry desde el nombre completo de una imagen.
    /// Ejemplo: "repos.daviviendahn.dvhn/davivienda-banco/myapp:latest" → "repos.daviviendahn.dvhn"
    /// Retorna null si no se puede extraer (imagen sin registry explícito).
    /// </summary>
    private static string? ExtractRegistryUrl(string imageName)
    {
        // Si contiene un punto o puerto antes del primer /, es un registry
        int slashIndex = imageName.IndexOf('/');
        if (slashIndex <= 0)
            return null;

        string potentialRegistry = imageName[..slashIndex];
        if (potentialRegistry.Contains('.') || potentialRegistry.Contains(':'))
            return potentialRegistry;

        return null;
    }
}
