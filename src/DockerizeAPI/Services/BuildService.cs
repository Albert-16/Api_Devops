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
/// Servicio principal de gestión de builds.
/// Crea registros en el BuildStore, los encola en el BuildChannel para procesamiento asíncrono,
/// y maneja consultas, cancelación y retry de builds.
/// </summary>
public sealed class BuildService : IBuildService
{
    private readonly BuildStore _store;
    private readonly BuildChannel _buildChannel;
    private readonly RegistrySettings _registrySettings;
    private readonly ILogger<BuildService> _logger;

    /// <summary>Inicializa el servicio con sus dependencias.</summary>
    public BuildService(
        BuildStore store,
        BuildChannel buildChannel,
        IOptions<RegistrySettings> registrySettings,
        ILogger<BuildService> logger)
    {
        _store = store;
        _buildChannel = buildChannel;
        _registrySettings = registrySettings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BuildResponse> CreateBuildAsync(CreateBuildRequest request, CancellationToken cancellationToken = default)
    {
        // Resolver valores con defaults del request y de la configuración
        string registryUrl = request.RegistryConfig?.RegistryUrl ?? _registrySettings.Url;
        string owner = request.RegistryConfig?.Owner ?? _registrySettings.Owner;
        string imageName = request.ImageConfig?.ImageName
            ?? ExtractRepoName(request.RepositoryUrl);
        string imageTag = request.ImageConfig?.Tag ?? request.Branch;
        bool includeOdbc = request.ImageConfig?.IncludeOdbcDependencies ?? false;

        var buildRecord = new BuildRecord
        {
            Id = Guid.NewGuid(),
            RepositoryUrl = request.RepositoryUrl,
            Branch = request.Branch,
            GitToken = request.GitToken,
            ImageName = imageName,
            ImageTag = imageTag,
            IncludeOdbcDependencies = includeOdbc,
            Status = BuildStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            Platform = request.ImageConfig?.Platform ?? "linux/amd64",
            RegistryUrl = registryUrl,
            RegistryOwner = owner,
            BuildArgs = request.ImageConfig?.BuildArgs,
            Labels = request.ImageConfig?.Labels,
            NoCache = request.ImageConfig?.NoCache ?? false,
            Pull = request.ImageConfig?.Pull ?? false,
            Quiet = request.ImageConfig?.Quiet ?? false,
            Network = request.ImageConfig?.Network ?? NetworkMode.Host,
            Progress = request.ImageConfig?.Progress ?? ProgressMode.Auto,
            OriginalRequestJson = JsonSerializer.Serialize(request)
        };

        _store.AddBuild(buildRecord);

        // Encolar para procesamiento asíncrono
        var channelRequest = new BuildChannelRequest(
            buildRecord.Id,
            request.RepositoryUrl,
            request.Branch,
            request.GitToken,
            imageName,
            imageTag,
            includeOdbc,
            registryUrl,
            owner);

        await _buildChannel.Writer.WriteAsync(channelRequest, cancellationToken);

        _logger.LogInformation(
            "Build {BuildId} encolado: {ImageName}:{ImageTag} desde {RepositoryUrl}",
            buildRecord.Id, imageName, imageTag, ProcessRunner.SanitizeForLogging(request.RepositoryUrl));

        return MapToResponse(buildRecord);
    }

    /// <inheritdoc/>
    public BuildDetailResponse? GetBuildById(Guid buildId)
    {
        BuildRecord? build = _store.GetBuild(buildId);
        if (build is null)
            return null;

        IReadOnlyList<BuildLog> logs = _store.GetLogs(buildId);

        return new BuildDetailResponse
        {
            BuildId = build.Id,
            Status = build.Status,
            RepositoryUrl = build.RepositoryUrl,
            Branch = build.Branch,
            CommitSha = build.CommitSha,
            ImageName = build.ImageName,
            ImageTag = build.ImageTag,
            IncludeOdbcDependencies = build.IncludeOdbcDependencies,
            ErrorMessage = build.ErrorMessage,
            GeneratedDockerfile = build.GeneratedDockerfile,
            CsprojPath = build.CsprojPath,
            AssemblyName = build.AssemblyName,
            CreatedAt = build.CreatedAt,
            StartedAt = build.StartedAt,
            CompletedAt = build.CompletedAt,
            ImageSizeBytes = build.ImageSizeBytes,
            RetryCount = build.RetryCount,
            ImageUrl = build.ImageUrl,
            Logs = logs.Select(l => new BuildLogEntry
            {
                Message = l.Message,
                Level = l.Level,
                Timestamp = l.Timestamp
            }).ToList()
        };
    }

    /// <inheritdoc/>
    public PagedResponse<BuildResponse> GetBuilds(
        int page = 1,
        int pageSize = 20,
        BuildStatus? status = null,
        string? branch = null,
        string? repositoryUrl = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        (IReadOnlyList<BuildRecord> items, int totalCount) = _store.GetBuilds(page, pageSize, status, branch, repositoryUrl);

        return new PagedResponse<BuildResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc/>
    public bool CancelBuild(Guid buildId)
    {
        BuildRecord? build = _store.GetBuild(buildId);
        if (build is null)
            return false;

        // Solo se pueden cancelar builds en estados activos
        if (build.Status is BuildStatus.Completed or BuildStatus.Failed or BuildStatus.Cancelled)
        {
            return false;
        }

        _store.UpdateBuild(buildId, b =>
        {
            b.Status = BuildStatus.Cancelled;
            b.CompletedAt = DateTimeOffset.UtcNow;
            b.ErrorMessage = "Build cancelado por el usuario.";
        });

        _logger.LogInformation("Build {BuildId} cancelado por el usuario", buildId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<BuildResponse?> RetryBuildAsync(Guid buildId, CancellationToken cancellationToken = default)
    {
        BuildRecord? build = _store.GetBuild(buildId);
        if (build is null)
            return null;

        // Solo se pueden reintentar builds fallidos
        if (build.Status != BuildStatus.Failed)
            return null;

        // Resetear el build para reintento
        _store.UpdateBuild(buildId, b =>
        {
            b.Status = BuildStatus.Queued;
            b.RetryCount++;
            b.ErrorMessage = null;
            b.StartedAt = null;
            b.CompletedAt = null;
            b.GeneratedDockerfile = null;
            b.CsprojPath = null;
            b.AssemblyName = null;
            b.CommitSha = null;
            b.ImageUrl = null;
            b.ImageSizeBytes = null;
        });

        // Re-encolar
        var channelRequest = new BuildChannelRequest(
            build.Id,
            build.RepositoryUrl,
            build.Branch,
            build.GitToken,
            build.ImageName,
            build.ImageTag,
            build.IncludeOdbcDependencies,
            build.RegistryUrl,
            build.RegistryOwner);

        await _buildChannel.Writer.WriteAsync(channelRequest, cancellationToken);

        _logger.LogInformation(
            "Build {BuildId} reintentado (intento #{RetryCount})",
            buildId, build.RetryCount);

        return MapToResponse(_store.GetBuild(buildId)!);
    }

    /// <summary>Mapea un BuildRecord a BuildResponse.</summary>
    private static BuildResponse MapToResponse(BuildRecord record)
    {
        return new BuildResponse
        {
            BuildId = record.Id,
            Status = record.Status,
            RepositoryUrl = record.RepositoryUrl,
            Branch = record.Branch,
            ImageName = record.ImageName,
            ImageTag = record.ImageTag,
            IncludeOdbcDependencies = record.IncludeOdbcDependencies,
            CreatedAt = record.CreatedAt,
            CompletedAt = record.CompletedAt,
            ImageUrl = record.ImageUrl
        };
    }

    /// <summary>
    /// Extrae el nombre del repositorio desde su URL.
    /// Ejemplo: "https://repos.daviviendahn.dvhn/davivienda-banco/ms23-auth.git" → "ms23-auth"
    /// </summary>
    private static string ExtractRepoName(string repositoryUrl)
    {
        string path = new Uri(repositoryUrl).AbsolutePath.TrimEnd('/');
        string name = path.Split('/').Last();
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name.ToLowerInvariant();
    }
}
