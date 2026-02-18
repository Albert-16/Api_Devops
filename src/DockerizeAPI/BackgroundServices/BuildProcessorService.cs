using System.Collections.Concurrent;
using System.Diagnostics;
using DockerizeAPI.Configuration;
using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Services;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DockerizeAPI.BackgroundServices;

/// <summary>
/// Background service que consume builds del BuildChannel y ejecuta el pipeline completo.
/// Pipeline: Clone → Detectar .csproj → Generar Dockerfile → Buildah build → Login → Push → Cleanup.
/// Soporta múltiples workers concurrentes controlados por MaxConcurrentBuilds.
/// Soporta cancelación individual de builds.
/// </summary>
public sealed class BuildProcessorService : BackgroundService
{
    private readonly BuildChannel _buildChannel;
    private readonly BuildStore _store;
    private readonly IGitService _gitService;
    private readonly IDockerfileGenerator _dockerfileGenerator;
    private readonly IBuildahService _buildahService;
    private readonly IBuildLogBroadcaster _broadcaster;
    private readonly BuildSettings _buildSettings;
    private readonly ILogger<BuildProcessorService> _logger;

    /// <summary>Tokens de cancelación por build para soportar cancelación individual.</summary>
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _buildCancellations = new();

    /// <summary>Inicializa el servicio con todas sus dependencias.</summary>
    public BuildProcessorService(
        BuildChannel buildChannel,
        BuildStore store,
        IGitService gitService,
        IDockerfileGenerator dockerfileGenerator,
        IBuildahService buildahService,
        IBuildLogBroadcaster broadcaster,
        IOptions<BuildSettings> buildSettings,
        ILogger<BuildProcessorService> logger)
    {
        _buildChannel = buildChannel;
        _store = store;
        _gitService = gitService;
        _dockerfileGenerator = dockerfileGenerator;
        _buildahService = buildahService;
        _broadcaster = broadcaster;
        _buildSettings = buildSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Loop principal que lee builds del canal y los procesa con concurrencia limitada.
    /// Usa SemaphoreSlim para limitar a MaxConcurrentBuilds workers simultáneos.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BuildProcessorService iniciado. Max builds concurrentes: {MaxConcurrent}. Timeout: {Timeout} min.",
            _buildSettings.MaxConcurrentBuilds,
            _buildSettings.TimeoutMinutes);

        using var semaphore = new SemaphoreSlim(_buildSettings.MaxConcurrentBuilds);

        await foreach (BuildChannelRequest request in _buildChannel.Reader.ReadAllAsync(stoppingToken))
        {
            // Verificar si el build fue cancelado mientras estaba en cola
            BuildRecord? build = _store.GetBuild(request.BuildId);
            if (build is null || build.Status == BuildStatus.Cancelled)
            {
                _logger.LogDebug("Build {BuildId} cancelado o no encontrado, ignorando", request.BuildId);
                continue;
            }

            await semaphore.WaitAsync(stoppingToken);

            // Lanzar procesamiento en background, liberando el semáforo al terminar
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessBuildAsync(request, stoppingToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, stoppingToken);
        }
    }

    /// <summary>
    /// Ejecuta el pipeline completo de un build individual.
    /// Cada paso actualiza el estado en el store y emite logs vía broadcaster.
    /// </summary>
    private async Task ProcessBuildAsync(BuildChannelRequest request, CancellationToken stoppingToken)
    {
        using var buildCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        buildCts.CancelAfter(TimeSpan.FromMinutes(_buildSettings.TimeoutMinutes));
        _buildCancellations[request.BuildId] = buildCts;

        CancellationToken ct = buildCts.Token;
        string workspacePath = Path.Combine(_buildSettings.TempDirectory, request.BuildId.ToString());
        string fullImageTag = $"{request.RegistryUrl}/{request.RegistryOwner}/{request.ImageName}:{request.ImageTag}";

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Procesando build {BuildId}: {ImageTag}", request.BuildId, fullImageTag);

        try
        {
            // ─── PASO 1: Clonar repositorio ───
            UpdateBuildStatus(request.BuildId, BuildStatus.Cloning);

            string tokenUrl = InjectTokenInUrl(request.RepositoryUrl, request.GitToken);
            await _gitService.CloneAsync(tokenUrl, request.Branch, workspacePath, request.BuildId, ct);

            // Obtener commit SHA
            string? commitSha = await _gitService.GetCurrentCommitShaAsync(workspacePath, ct);
            _store.UpdateBuild(request.BuildId, b => b.CommitSha = commitSha);

            // ─── PASO 2: Detectar .csproj ───
            string csprojPath;
            BuildRecord? buildRecord = _store.GetBuild(request.BuildId);

            // Usar csprojPath del request original si fue proporcionado
            string? providedCsprojPath = null;
            if (buildRecord?.OriginalRequestJson is not null)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(buildRecord.OriginalRequestJson);
                    if (doc.RootElement.TryGetProperty("imageConfig", out var imageConfig) &&
                        imageConfig.TryGetProperty("csprojPath", out var csprojProp))
                    {
                        providedCsprojPath = csprojProp.GetString();
                    }
                }
                catch { /* Si falla el parsing, usar autodetección */ }
            }

            if (!string.IsNullOrEmpty(providedCsprojPath))
            {
                string fullCsprojPath = Path.Combine(workspacePath, providedCsprojPath);
                if (!File.Exists(fullCsprojPath))
                {
                    throw new InvalidOperationException(
                        $"El archivo .csproj especificado no existe: {providedCsprojPath}");
                }
                csprojPath = providedCsprojPath;
                await _broadcaster.BroadcastLogAsync(request.BuildId,
                    $"Usando csprojPath proporcionado: {csprojPath}", cancellationToken: ct);
            }
            else
            {
                csprojPath = await _gitService.DetectCsprojAsync(workspacePath);
            }

            // Extraer AssemblyName
            string csprojFullPath = Path.Combine(workspacePath, csprojPath);
            string? assemblyName = _gitService.ExtractAssemblyName(csprojFullPath);
            string resolvedAssemblyName = assemblyName ?? Path.GetFileNameWithoutExtension(csprojPath);

            _store.UpdateBuild(request.BuildId, b =>
            {
                b.CsprojPath = csprojPath;
                b.AssemblyName = resolvedAssemblyName;
            });

            // ─── PASO 3: Generar Dockerfile ───
            UpdateBuildStatus(request.BuildId, BuildStatus.Building);

            string dockerfile = _dockerfileGenerator.Generate(
                request.IncludeOdbcDependencies,
                csprojPath,
                assemblyName);

            _store.UpdateBuild(request.BuildId, b => b.GeneratedDockerfile = dockerfile);

            // Escribir Dockerfile en el workspace
            string dockerfilePath = Path.Combine(workspacePath, "Dockerfile");
            await File.WriteAllTextAsync(dockerfilePath, dockerfile, ct);

            await _broadcaster.BroadcastLogAsync(request.BuildId,
                "Dockerfile generado y escrito en el workspace", cancellationToken: ct);

            // ─── PASO 4: Buildah build ───
            var buildOptions = new BuildahBuildOptions
            {
                Platform = buildRecord?.Platform ?? "linux/amd64",
                NoCache = buildRecord?.NoCache ?? false,
                Pull = buildRecord?.Pull ?? false,
                Quiet = buildRecord?.Quiet ?? false,
                Network = (buildRecord?.Network ?? NetworkMode.Bridge) switch
                {
                    NetworkMode.Host => "host",
                    NetworkMode.None => "none",
                    _ => "bridge"
                },
                BuildArgs = BuildMergedBuildArgs(request),
                Labels = buildRecord?.Labels
            };

            bool buildSuccess = await _buildahService.BuildImageAsync(
                workspacePath, dockerfilePath, fullImageTag, request.BuildId, buildOptions, ct);

            if (!buildSuccess)
            {
                throw new InvalidOperationException("Error durante la construcción de la imagen con Buildah.");
            }

            // ─── PASO 5: Login + Push ───
            UpdateBuildStatus(request.BuildId, BuildStatus.Pushing);

            bool loginSuccess = await _buildahService.LoginAsync(
                request.RegistryUrl, "token", request.GitToken, request.BuildId, ct);

            if (!loginSuccess)
            {
                throw new InvalidOperationException("Autenticación al registry fallida. Verifique el gitToken.");
            }

            bool pushSuccess = await _buildahService.PushImageAsync(fullImageTag, request.BuildId, ct);

            if (!pushSuccess)
            {
                throw new InvalidOperationException("Error al publicar la imagen al registry.");
            }

            // ─── PASO 6: Completar ───
            stopwatch.Stop();

            _store.UpdateBuild(request.BuildId, b =>
            {
                b.Status = BuildStatus.Completed;
                b.CompletedAt = DateTimeOffset.UtcNow;
                b.ImageUrl = fullImageTag;
            });

            _store.AddLog(request.BuildId, new BuildLog
            {
                BuildRecordId = request.BuildId,
                Message = $"Build completado exitosamente en {stopwatch.Elapsed.TotalSeconds:F1}s. Imagen: {fullImageTag}",
                Level = "info"
            });

            await _broadcaster.BroadcastLogAsync(request.BuildId,
                $"Build completado exitosamente en {stopwatch.Elapsed.TotalSeconds:F1}s. Imagen: {fullImageTag}",
                cancellationToken: CancellationToken.None);

            _logger.LogInformation(
                "Build completado {BuildId} en {Duration}ms. Imagen: {ImageUrl}",
                request.BuildId, stopwatch.ElapsedMilliseconds, fullImageTag);
        }
        catch (OperationCanceledException) when (buildCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            // Timeout o cancelación del build individual
            stopwatch.Stop();
            string message = $"Timeout al procesar build (>{_buildSettings.TimeoutMinutes} min)";

            _store.UpdateBuild(request.BuildId, b =>
            {
                b.Status = BuildStatus.Failed;
                b.CompletedAt = DateTimeOffset.UtcNow;
                b.ErrorMessage = message;
            });

            _store.AddLog(request.BuildId, new BuildLog
            {
                BuildRecordId = request.BuildId,
                Message = message,
                Level = "error"
            });

            await _broadcaster.BroadcastLogAsync(request.BuildId, message, "error", CancellationToken.None);
            _logger.LogWarning("Build {BuildId} timeout después de {Duration}ms", request.BuildId, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // Shutdown de la aplicación
            _store.UpdateBuild(request.BuildId, b =>
            {
                b.Status = BuildStatus.Cancelled;
                b.CompletedAt = DateTimeOffset.UtcNow;
                b.ErrorMessage = "Build cancelado por cierre de la aplicación.";
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            string errorMessage = ex.Message;

            _store.UpdateBuild(request.BuildId, b =>
            {
                b.Status = BuildStatus.Failed;
                b.CompletedAt = DateTimeOffset.UtcNow;
                b.ErrorMessage = errorMessage;
            });

            _store.AddLog(request.BuildId, new BuildLog
            {
                BuildRecordId = request.BuildId,
                Message = $"Build fallido: {errorMessage}",
                Level = "error"
            });

            await _broadcaster.BroadcastLogAsync(request.BuildId,
                $"Build fallido: {errorMessage}", "error", CancellationToken.None);

            _logger.LogError(ex,
                "Build fallido {BuildId} después de {Duration}ms",
                request.BuildId, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            // Cleanup
            _buildCancellations.TryRemove(request.BuildId, out _);
            await _broadcaster.CompleteBuildAsync(request.BuildId);

            // Limpiar workspace
            try
            {
                if (Directory.Exists(workspacePath))
                {
                    Directory.Delete(workspacePath, recursive: true);
                    _logger.LogDebug("Workspace limpiado: {Path}", workspacePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error limpiando workspace {Path}", workspacePath);
            }

            // Limpiar imagen local
            try
            {
                await _buildahService.CleanupImageAsync(fullImageTag, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error limpiando imagen local {ImageTag}", fullImageTag);
            }

            _store.AddLog(request.BuildId, new BuildLog
            {
                BuildRecordId = request.BuildId,
                Message = $"Cleanup completado para build {request.BuildId}",
                Level = "info"
            });
        }
    }

    /// <summary>
    /// Intenta cancelar un build en progreso.
    /// Llamado externamente cuando el usuario cancela un build.
    /// </summary>
    /// <param name="buildId">ID del build a cancelar.</param>
    /// <returns>true si se encontró y canceló el token.</returns>
    public bool TryCancelBuild(Guid buildId)
    {
        if (_buildCancellations.TryRemove(buildId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>Actualiza el estado de un build en el store y loguea el cambio.</summary>
    private void UpdateBuildStatus(Guid buildId, BuildStatus newStatus)
    {
        _store.UpdateBuild(buildId, b =>
        {
            b.Status = newStatus;
            if (newStatus != BuildStatus.Queued && b.StartedAt is null)
            {
                b.StartedAt = DateTimeOffset.UtcNow;
            }
        });

        _store.AddLog(buildId, new BuildLog
        {
            BuildRecordId = buildId,
            Message = $"Estado actualizado a: {newStatus}",
            Level = "info"
        });

        _logger.LogDebug("Build {BuildId} → {Status}", buildId, newStatus);
    }

    /// <summary>
    /// Inyecta el token de autenticación en la URL del repositorio.
    /// Ejemplo: https://repos.dvhn/org/repo.git → https://token@repos.dvhn/org/repo.git
    /// </summary>
    private static string InjectTokenInUrl(string repositoryUrl, string gitToken)
    {
        // No inyectar token en rutas locales o file://
        if (!repositoryUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !repositoryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return repositoryUrl;
        }

        var uri = new Uri(repositoryUrl);
        return $"{uri.Scheme}://{gitToken}@{uri.Host}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "")}{uri.PathAndQuery}";
    }

    /// <summary>
    /// Construye los build args combinando REPO_TOKEN (si ODBC) con args adicionales del request.
    /// </summary>
    private Dictionary<string, string>? BuildMergedBuildArgs(BuildChannelRequest request)
    {
        var args = new Dictionary<string, string>();

        // Si ODBC, agregar REPO_TOKEN para el repo Debian
        if (request.IncludeOdbcDependencies)
        {
            args["REPO_TOKEN"] = request.GitToken;
        }

        // Agregar build args adicionales del BuildRecord
        BuildRecord? record = _store.GetBuild(request.BuildId);
        if (record?.BuildArgs is not null)
        {
            foreach (KeyValuePair<string, string> arg in record.BuildArgs)
            {
                args[arg.Key] = arg.Value;
            }
        }

        return args.Count > 0 ? args : null;
    }
}
