using DockerizeAPI.Models;
using DockerizeAPI.Models.Configuration;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Services;
using Microsoft.Extensions.Options;

namespace DockerizeAPI.BackgroundServices;

/// <summary>
/// Servicio en segundo plano que procesa los builds encolados.
/// Consume builds del Channel administrado por BuildService y ejecuta
/// el pipeline completo: clonar, detectar proyecto, generar Dockerfile,
/// construir imagen, login al registry, push, y limpieza.
/// Soporta procesamiento paralelo limitado por MaxConcurrentBuilds.
/// </summary>
public class BuildProcessorService : BackgroundService
{
    private readonly BuildService _buildService;
    private readonly GitService _gitService;
    private readonly ProjectDetector _projectDetector;
    private readonly DockerfileGenerator _dockerfileGenerator;
    private readonly BuildahService _buildahService;
    private readonly BuildSettings _buildSettings;
    private readonly ILogger<BuildProcessorService> _logger;

    /// <summary>
    /// Inicializa el servicio de procesamiento de builds.
    /// </summary>
    public BuildProcessorService(
        BuildService buildService,
        GitService gitService,
        ProjectDetector projectDetector,
        DockerfileGenerator dockerfileGenerator,
        BuildahService buildahService,
        IOptions<BuildSettings> buildSettings,
        ILogger<BuildProcessorService> logger)
    {
        _buildService = buildService;
        _gitService = gitService;
        _projectDetector = projectDetector;
        _dockerfileGenerator = dockerfileGenerator;
        _buildahService = buildahService;
        _buildSettings = buildSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Metodo principal del Background Service.
    /// Lee builds del canal indefinidamente y los procesa con un SemaphoreSlim
    /// para limitar la concurrencia al maximo configurado.
    /// </summary>
    /// <param name="stoppingToken">Token de cancelacion del host.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BuildProcessorService iniciado. Max builds simultaneos: {Max}",
            _buildSettings.MaxConcurrentBuilds);

        // Semaphore para limitar builds simultaneos
        using var semaphore = new SemaphoreSlim(_buildSettings.MaxConcurrentBuilds);

        await foreach (var buildInfo in _buildService.BuildChannelReader.ReadAllAsync(stoppingToken))
        {
            await semaphore.WaitAsync(stoppingToken);

            // Procesar cada build en un Task separado para permitir paralelismo
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessBuildAsync(buildInfo, stoppingToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, stoppingToken);
        }
    }

    /// <summary>
    /// Procesa un build completo ejecutando todos los pasos del pipeline.
    /// Cada paso actualiza el estado del build y agrega logs.
    /// Si algun paso falla, el build se marca como Failed con el mensaje de error.
    /// </summary>
    /// <param name="buildInfo">Informacion del build a procesar.</param>
    /// <param name="hostToken">Token de cancelacion del host.</param>
    private async Task ProcessBuildAsync(BuildInfo buildInfo, CancellationToken hostToken)
    {
        _buildService.IncrementActiveBuilds();

        // Combinar el token de cancelacion del host con el del build individual
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            hostToken, buildInfo.CancellationTokenSource.Token);

        // Aplicar timeout configurable
        linkedCts.CancelAfter(TimeSpan.FromMinutes(_buildSettings.TimeoutMinutes));

        var cancellationToken = linkedCts.Token;
        var workDir = Path.Combine(_buildSettings.TempDirectory, $"build-{buildInfo.BuildId}");

        // Callback para agregar logs al build en tiempo real
        void AddLog(string message)
        {
            var timestamped = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
            buildInfo.Logs.Add(timestamped);
        }

        try
        {
            AddLog($"=== Iniciando build {buildInfo.BuildId} ===");
            AddLog($"Repositorio: {buildInfo.RepositoryUrl}");
            AddLog($"Rama: {buildInfo.Branch}");
            AddLog($"ODBC: {buildInfo.ImageConfig.IncludeOdbcDependencies}");

            // ===== PASO 1: Clonar repositorio =====
            buildInfo.Status = BuildStatus.Cloning;
            buildInfo.CloningStartedAt = DateTime.UtcNow;
            AddLog("--- Paso 1: Clonando repositorio ---");

            await _gitService.CloneAsync(
                buildInfo.RepositoryUrl,
                buildInfo.Branch,
                buildInfo.GitToken,
                workDir,
                AddLog,
                cancellationToken);

            buildInfo.WorkDirectory = workDir;

            // ===== PASO 2: Detectar proyecto .NET =====
            AddLog("--- Paso 2: Detectando proyecto .NET ---");
            var projectInfo = _projectDetector.Detect(workDir);
            buildInfo.CsprojPath = projectInfo.CsprojPath;
            buildInfo.AssemblyName = projectInfo.AssemblyName;
            AddLog($"Proyecto detectado: {projectInfo.CsprojPath} (Assembly: {projectInfo.AssemblyName})");

            // ===== PASO 3: Generar Dockerfile =====
            AddLog("--- Paso 3: Generando Dockerfile ---");
            var dockerfileContent = _dockerfileGenerator.Generate(
                buildInfo.ImageConfig.IncludeOdbcDependencies,
                projectInfo.CsprojPath,
                projectInfo.AssemblyName);

            var dockerfilePath = Path.Combine(workDir, "Dockerfile");
            await File.WriteAllTextAsync(dockerfilePath, dockerfileContent, cancellationToken);
            AddLog("Dockerfile generado y escrito en el directorio de build.");

            // ===== PASO 4: Construir imagen con Buildah =====
            buildInfo.Status = BuildStatus.Building;
            buildInfo.BuildingStartedAt = DateTime.UtcNow;
            AddLog("--- Paso 4: Construyendo imagen con Buildah ---");

            var imageTag = $"{buildInfo.RegistryConfig.RegistryUrl}/{buildInfo.RegistryConfig.Owner}/{buildInfo.ImageConfig.ImageName}:{buildInfo.ImageConfig.Tag}";

            await _buildahService.BuildAsync(
                imageTag,
                workDir,
                dockerfilePath,
                buildInfo.ImageConfig,
                buildInfo.GitToken,
                AddLog,
                cancellationToken);

            // ===== PASO 5: Login al registry =====
            buildInfo.Status = BuildStatus.Pushing;
            buildInfo.PushingStartedAt = DateTime.UtcNow;
            AddLog("--- Paso 5: Autenticandose en el registry ---");

            await _buildahService.LoginAsync(
                buildInfo.RegistryConfig.RegistryUrl,
                "token",
                buildInfo.GitToken,
                AddLog,
                cancellationToken);

            // ===== PASO 6: Push al registry =====
            AddLog("--- Paso 6: Subiendo imagen al registry ---");

            await _buildahService.PushAsync(imageTag, AddLog, cancellationToken);

            // ===== BUILD COMPLETADO =====
            buildInfo.Status = BuildStatus.Completed;
            buildInfo.CompletedAt = DateTime.UtcNow;
            buildInfo.ImageUrl = imageTag;
            AddLog($"=== Build completado exitosamente ===");
            AddLog($"Imagen: {imageTag}");

            _logger.LogInformation("Build {BuildId} completado: {ImageTag}", buildInfo.BuildId, imageTag);
        }
        catch (OperationCanceledException)
        {
            buildInfo.Status = buildInfo.Status == BuildStatus.Cancelled
                ? BuildStatus.Cancelled
                : BuildStatus.Failed;
            buildInfo.CompletedAt = DateTime.UtcNow;
            buildInfo.ErrorMessage = buildInfo.Status == BuildStatus.Cancelled
                ? "Build cancelado por el usuario."
                : $"Build excedio el timeout de {_buildSettings.TimeoutMinutes} minutos.";
            AddLog($"[ERROR] {buildInfo.ErrorMessage}");

            _logger.LogWarning("Build {BuildId} cancelado/timeout", buildInfo.BuildId);
        }
        catch (Exception ex)
        {
            buildInfo.Status = BuildStatus.Failed;
            buildInfo.CompletedAt = DateTime.UtcNow;
            buildInfo.ErrorMessage = ex.Message;
            AddLog($"[ERROR] Build fallido: {ex.Message}");

            _logger.LogError(ex, "Build {BuildId} fallido", buildInfo.BuildId);
        }
        finally
        {
            _buildService.DecrementActiveBuilds();

            // ===== LIMPIEZA: Eliminar directorio temporal e imagen local =====
            AddLog("--- Limpieza de recursos ---");

            if (Directory.Exists(workDir))
            {
                try
                {
                    Directory.Delete(workDir, recursive: true);
                    AddLog("Directorio temporal eliminado.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo eliminar el directorio temporal {Dir}", workDir);
                    AddLog($"Advertencia: No se pudo eliminar directorio temporal: {ex.Message}");
                }
            }

            // Intentar eliminar la imagen local si el build llego a construirla
            if (buildInfo.ImageUrl is not null)
            {
                await _buildahService.RemoveImageAsync(buildInfo.ImageUrl, AddLog);
            }
        }
    }
}
