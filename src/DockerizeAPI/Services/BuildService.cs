using System.Collections.Concurrent;
using System.Threading.Channels;
using DockerizeAPI.Models;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using Microsoft.Extensions.Options;
using DockerizeAPI.Models.Configuration;

namespace DockerizeAPI.Services;

/// <summary>
/// Servicio central de gestion de builds.
/// Administra el almacenamiento en memoria de todos los builds,
/// la cola de procesamiento (Channel), y proporciona metodos
/// para crear, consultar, cancelar y reintentar builds.
/// </summary>
public class BuildService
{
    /// <summary>
    /// Almacen en memoria de todos los builds, indexados por BuildId.
    /// Thread-safe para acceso concurrente.
    /// </summary>
    private readonly ConcurrentDictionary<string, BuildInfo> _builds = new();

    /// <summary>
    /// Canal de comunicacion entre los endpoints y el Background Service.
    /// Los builds se encolan aqui y el BuildProcessorService los consume.
    /// </summary>
    private readonly Channel<BuildInfo> _buildChannel;

    private readonly RegistrySettings _registrySettings;
    private readonly BuildSettings _buildSettings;
    private readonly ILogger<BuildService> _logger;

    /// <summary>
    /// Contador de builds actualmente en ejecucion.
    /// Se usa para controlar el rate limiting (maximo N builds simultaneos).
    /// </summary>
    private int _activeBuilds;

    /// <summary>
    /// Inicializa el servicio de builds.
    /// </summary>
    /// <param name="registrySettings">Configuracion del Container Registry.</param>
    /// <param name="buildSettings">Configuracion general de builds.</param>
    /// <param name="logger">Logger para registrar operaciones.</param>
    public BuildService(
        IOptions<RegistrySettings> registrySettings,
        IOptions<BuildSettings> buildSettings,
        ILogger<BuildService> logger)
    {
        _registrySettings = registrySettings.Value;
        _buildSettings = buildSettings.Value;
        _logger = logger;

        // Canal sin limite de capacidad para no bloquear requests entrantes
        _buildChannel = Channel.CreateUnbounded<BuildInfo>(new UnboundedChannelOptions
        {
            SingleReader = false
        });
    }

    /// <summary>
    /// Reader del canal de builds. Utilizado por el BuildProcessorService
    /// para consumir builds de la cola.
    /// </summary>
    public ChannelReader<BuildInfo> BuildChannelReader => _buildChannel.Reader;

    /// <summary>
    /// Cantidad de builds actualmente en ejecucion.
    /// </summary>
    public int ActiveBuilds => _activeBuilds;

    /// <summary>
    /// Maximo de builds simultaneos configurado.
    /// </summary>
    public int MaxConcurrentBuilds => _buildSettings.MaxConcurrentBuilds;

    /// <summary>
    /// Crea un nuevo build y lo encola para procesamiento.
    /// Valida que no se exceda el limite de builds simultaneos.
    /// </summary>
    /// <param name="request">Request con la configuracion del build.</param>
    /// <returns>Respuesta con el buildId y estado Queued.</returns>
    /// <exception cref="InvalidOperationException">Si se excede el limite de builds simultaneos.</exception>
    public async Task<BuildResponse> CreateBuildAsync(CreateBuildRequest request)
    {
        // Validar rate limiting
        if (_activeBuilds >= _buildSettings.MaxConcurrentBuilds)
        {
            throw new InvalidOperationException(
                $"Se alcanzo el limite de {_buildSettings.MaxConcurrentBuilds} builds simultaneos. " +
                "Intente de nuevo mas tarde.");
        }

        // Extraer nombre del repositorio de la URL
        var repoName = ExtractRepoName(request.RepositoryUrl);

        // Construir configuracion de imagen con valores por defecto
        var imageConfig = request.ImageConfig ?? new ImageConfig();
        imageConfig.ImageName ??= repoName;
        imageConfig.Tag ??= SanitizeTag(request.Branch);

        // Construir configuracion del registry con valores por defecto
        var registryConfig = request.RegistryConfig ?? new RegistryConfig();
        registryConfig.RegistryUrl ??= _registrySettings.Url;
        registryConfig.Owner ??= _registrySettings.Owner;
        registryConfig.Repository ??= repoName;

        var buildInfo = new BuildInfo
        {
            RepositoryUrl = request.RepositoryUrl,
            Branch = request.Branch,
            GitToken = request.GitToken,
            ImageConfig = imageConfig,
            RegistryConfig = registryConfig
        };

        _builds[buildInfo.BuildId] = buildInfo;

        // Encolar el build para procesamiento por el Background Service
        await _buildChannel.Writer.WriteAsync(buildInfo);

        _logger.LogInformation(
            "Build {BuildId} creado y encolado: {RepoUrl} rama {Branch}",
            buildInfo.BuildId, request.RepositoryUrl, request.Branch);

        return MapToResponse(buildInfo);
    }

    /// <summary>
    /// Obtiene la informacion de un build por su ID.
    /// </summary>
    /// <param name="buildId">Identificador del build.</param>
    /// <returns>Respuesta del build o null si no existe.</returns>
    public BuildResponse? GetBuild(string buildId)
    {
        return _builds.TryGetValue(buildId, out var build) ? MapToResponse(build) : null;
    }

    /// <summary>
    /// Obtiene la entidad BuildInfo completa (uso interno por el Background Service).
    /// </summary>
    /// <param name="buildId">Identificador del build.</param>
    /// <returns>BuildInfo o null si no existe.</returns>
    public BuildInfo? GetBuildInfo(string buildId)
    {
        return _builds.TryGetValue(buildId, out var build) ? build : null;
    }

    /// <summary>
    /// Lista todos los builds con filtros opcionales y paginacion.
    /// </summary>
    /// <param name="page">Numero de pagina (base 1).</param>
    /// <param name="pageSize">Items por pagina.</param>
    /// <param name="status">Filtro por estado (opcional).</param>
    /// <param name="branch">Filtro por rama (opcional).</param>
    /// <param name="repositoryUrl">Filtro por URL de repositorio (opcional).</param>
    /// <returns>Respuesta paginada con los builds.</returns>
    public BuildListResponse GetBuilds(
        int page = 1,
        int pageSize = 20,
        BuildStatus? status = null,
        string? branch = null,
        string? repositoryUrl = null)
    {
        var query = _builds.Values.AsEnumerable();

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (!string.IsNullOrEmpty(branch))
            query = query.Where(b => b.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(repositoryUrl))
            query = query.Where(b => b.RepositoryUrl.Contains(repositoryUrl, StringComparison.OrdinalIgnoreCase));

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponse)
            .ToList();

        return new BuildListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Cancela un build en progreso.
    /// Solo se pueden cancelar builds que esten en estado Queued, Cloning, Building o Pushing.
    /// </summary>
    /// <param name="buildId">Identificador del build a cancelar.</param>
    /// <returns>True si se cancelo, false si no existe o ya finalizo.</returns>
    public bool CancelBuild(string buildId)
    {
        if (!_builds.TryGetValue(buildId, out var build))
            return false;

        if (build.Status is BuildStatus.Completed or BuildStatus.Failed or BuildStatus.Cancelled)
            return false;

        build.CancellationTokenSource.Cancel();
        build.Status = BuildStatus.Cancelled;
        build.CompletedAt = DateTime.UtcNow;
        build.ErrorMessage = "Build cancelado por el usuario.";
        build.Logs.Add("[SISTEMA] Build cancelado por el usuario.");

        _logger.LogInformation("Build {BuildId} cancelado", buildId);
        return true;
    }

    /// <summary>
    /// Reintenta un build fallido creando un nuevo build con la misma configuracion.
    /// </summary>
    /// <param name="buildId">Identificador del build fallido a reintentar.</param>
    /// <returns>Respuesta del nuevo build o null si el build original no existe o no fallo.</returns>
    public async Task<BuildResponse?> RetryBuildAsync(string buildId)
    {
        if (!_builds.TryGetValue(buildId, out var original))
            return null;

        if (original.Status != BuildStatus.Failed)
            return null;

        var retryRequest = new CreateBuildRequest
        {
            RepositoryUrl = original.RepositoryUrl,
            Branch = original.Branch,
            GitToken = original.GitToken,
            ImageConfig = original.ImageConfig,
            RegistryConfig = original.RegistryConfig
        };

        _logger.LogInformation("Reintentando build {BuildId}", buildId);
        return await CreateBuildAsync(retryRequest);
    }

    /// <summary>
    /// Obtiene los logs completos de un build.
    /// </summary>
    /// <param name="buildId">Identificador del build.</param>
    /// <returns>Lista de lineas de log o null si el build no existe.</returns>
    public List<string>? GetBuildLogs(string buildId)
    {
        if (!_builds.TryGetValue(buildId, out var build))
            return null;

        return [.. build.Logs];
    }

    /// <summary>
    /// Incrementa el contador de builds activos. Llamado por el Background Service al iniciar un build.
    /// </summary>
    public void IncrementActiveBuilds() => Interlocked.Increment(ref _activeBuilds);

    /// <summary>
    /// Decrementa el contador de builds activos. Llamado por el Background Service al finalizar un build.
    /// </summary>
    public void DecrementActiveBuilds() => Interlocked.Decrement(ref _activeBuilds);

    /// <summary>
    /// Mapea una entidad BuildInfo a una respuesta BuildResponse.
    /// </summary>
    private static BuildResponse MapToResponse(BuildInfo build)
    {
        return new BuildResponse
        {
            BuildId = build.BuildId,
            Status = build.Status,
            RepositoryUrl = build.RepositoryUrl,
            Branch = build.Branch,
            ImageUrl = build.ImageUrl,
            IncludeOdbcDependencies = build.ImageConfig.IncludeOdbcDependencies,
            CreatedAt = build.CreatedAt,
            CloningStartedAt = build.CloningStartedAt,
            BuildingStartedAt = build.BuildingStartedAt,
            PushingStartedAt = build.PushingStartedAt,
            CompletedAt = build.CompletedAt,
            ErrorMessage = build.ErrorMessage,
            RecentLogs = build.Logs.TakeLast(20).ToList()
        };
    }

    /// <summary>
    /// Extrae el nombre del repositorio de la URL.
    /// Ejemplo: "https://repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web" -> "ms23-autenticacion-web"
    /// </summary>
    private static string ExtractRepoName(string url)
    {
        var uri = new Uri(url.TrimEnd('/'));
        var lastSegment = uri.Segments.Last().TrimEnd('/');
        // Eliminar .git si esta presente
        return lastSegment.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? lastSegment[..^4]
            : lastSegment;
    }

    /// <summary>
    /// Sanitiza un nombre de rama para usarlo como tag de imagen Docker.
    /// Reemplaza caracteres no validos con guiones.
    /// </summary>
    private static string SanitizeTag(string branch)
    {
        return branch.Replace('/', '-').Replace('\\', '-').ToLowerInvariant();
    }
}
