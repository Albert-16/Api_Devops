using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using DockerizeAPI.Configuration;
using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Services;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DockerizeAPI.BackgroundServices;

/// <summary>
/// Background service que consume deploys del DeployChannel y ejecuta el pipeline completo.
/// Pipeline: Login → Pull → (Stop+Rm si existe) → Run → Verify → Running (o auto-rollback).
/// Soporta múltiples workers concurrentes controlados por MaxConcurrentDeploys.
/// </summary>
public sealed class DeployProcessorService : BackgroundService
{
    private readonly DeployChannel _deployChannel;
    private readonly DeployStore _store;
    private readonly IDockerRunService _dockerRunService;
    private readonly IDeployLogBroadcaster _broadcaster;
    private readonly DeploySettings _deploySettings;
    private readonly ILogger<DeployProcessorService> _logger;

    /// <summary>Tokens de cancelación por deploy para soportar timeout individual.</summary>
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _deployCancellations = new();

    /// <summary>Inicializa el servicio con todas sus dependencias.</summary>
    public DeployProcessorService(
        DeployChannel deployChannel,
        DeployStore store,
        IDockerRunService dockerRunService,
        IDeployLogBroadcaster broadcaster,
        IOptions<DeploySettings> deploySettings,
        ILogger<DeployProcessorService> logger)
    {
        _deployChannel = deployChannel;
        _store = store;
        _dockerRunService = dockerRunService;
        _broadcaster = broadcaster;
        _deploySettings = deploySettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Loop principal que lee deploys del canal y los procesa con concurrencia limitada.
    /// Usa SemaphoreSlim para limitar a MaxConcurrentDeploys workers simultáneos.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DeployProcessorService iniciado. Max deploys concurrentes: {MaxConcurrent}. Timeout: {Timeout} min.",
            _deploySettings.MaxConcurrentDeploys,
            _deploySettings.TimeoutMinutes);

        using var semaphore = new SemaphoreSlim(_deploySettings.MaxConcurrentDeploys);

        await foreach (DeployChannelRequest request in _deployChannel.Reader.ReadAllAsync(stoppingToken))
        {
            // Verificar si el deploy fue cancelado mientras estaba en cola
            DeployRecord? deploy = _store.GetDeploy(request.DeployId);
            if (deploy is null || deploy.Status == DeployStatus.Cancelled)
            {
                _logger.LogDebug("Deploy {DeployId} cancelado o no encontrado, ignorando", request.DeployId);
                continue;
            }

            await semaphore.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessDeployAsync(request, stoppingToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, stoppingToken);
        }
    }

    /// <summary>
    /// Ejecuta el pipeline completo de un deploy individual.
    /// Incluye auto-rollback si el container falla al arrancar.
    /// </summary>
    private async Task ProcessDeployAsync(DeployChannelRequest request, CancellationToken stoppingToken)
    {
        using var deployCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        deployCts.CancelAfter(TimeSpan.FromMinutes(_deploySettings.TimeoutMinutes));
        _deployCancellations[request.DeployId] = deployCts;

        CancellationToken ct = deployCts.Token;

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Procesando deploy {DeployId}: {ImageName} → {ContainerName}",
            request.DeployId, request.ImageName, request.ContainerName);

        try
        {
            // Cargar el DeployRecord completo del store
            DeployRecord? deployRecord = _store.GetDeploy(request.DeployId);
            if (deployRecord is null)
            {
                _logger.LogWarning("Deploy {DeployId} no encontrado en store", request.DeployId);
                return;
            }

            // ─── PASO 1: Login al registry ───
            UpdateDeployStatus(request.DeployId, DeployStatus.LoggingIn);

            bool loginSuccess = await _dockerRunService.LoginAsync(
                request.RegistryUrl, "token", request.GitToken, request.DeployId, ct);

            if (!loginSuccess)
            {
                throw new InvalidOperationException("Autenticación al registry fallida. Verifique el gitToken.");
            }

            // ─── PASO 2: Pull de la imagen ───
            UpdateDeployStatus(request.DeployId, DeployStatus.Pulling);

            bool pullSuccess = await _dockerRunService.PullImageAsync(request.ImageName, request.DeployId, ct);

            if (!pullSuccess)
            {
                throw new InvalidOperationException($"No se pudo descargar la imagen: {request.ImageName}");
            }

            // ─── PASO 3: Deploy (stop+rm si existe, luego run) ───
            UpdateDeployStatus(request.DeployId, DeployStatus.Deploying);

            // Verificar si ya existe un container con ese nombre
            bool containerExists = await _dockerRunService.ContainerExistsAsync(request.ContainerName, ct);

            if (containerExists)
            {
                await _broadcaster.BroadcastLogAsync(request.DeployId,
                    $"Container existente detectado: {request.ContainerName}. Reemplazando...",
                    cancellationToken: ct);

                // Guardar info del container actual para rollback
                string? currentInspect = await _dockerRunService.InspectContainerAsync(request.ContainerName, ct);
                if (currentInspect is not null)
                {
                    // Intentar extraer la imagen actual del inspect para rollback
                    string? currentImage = ExtractImageFromInspect(currentInspect);
                    if (currentImage is not null)
                    {
                        _store.UpdateDeploy(request.DeployId, d =>
                        {
                            d.PreviousImageName = currentImage;
                            d.PreviousConfigJson = d.OriginalRequestJson; // Guardar config actual
                            d.DeployVersion++;
                        });

                        await _broadcaster.BroadcastLogAsync(request.DeployId,
                            $"Imagen anterior guardada para rollback: {currentImage}",
                            cancellationToken: ct);
                    }
                }

                // Stop + Remove
                await _dockerRunService.StopContainerAsync(request.ContainerName, request.DeployId, ct);
                await _dockerRunService.RemoveContainerAsync(request.ContainerName, request.DeployId, ct);
            }

            // Construir opciones de docker run
            DockerRunOptions runOptions = BuildRunOptions(deployRecord);

            // Loguear el comando generado para verificación
            string dockerCommand = $"docker {DockerRunService.BuildDockerRunArguments(runOptions)}";
            await _broadcaster.BroadcastLogAsync(request.DeployId,
                $"Comando: {dockerCommand}", cancellationToken: ct);
            _logger.LogInformation("Deploy {DeployId} comando: {DockerCommand}", request.DeployId, dockerCommand);

            // Ejecutar docker run
            string? containerId = await _dockerRunService.RunContainerAsync(runOptions, request.DeployId, ct);

            if (containerId is null)
            {
                // Run falló — intentar auto-rollback si hay imagen anterior
                await TryAutoRollbackAsync(request, deployRecord, ct);
                return;
            }

            // Guardar container ID
            _store.UpdateDeploy(request.DeployId, d => d.ContainerId = containerId);

            // Verificar que el container está corriendo
            // Esperar un momento para que Docker reporte el estado
            await Task.Delay(2000, ct);
            bool isRunning = await _dockerRunService.IsContainerRunningAsync(request.ContainerName, ct);

            if (!isRunning)
            {
                await _broadcaster.BroadcastLogAsync(request.DeployId,
                    "Container no está corriendo después del arranque. Verificando logs...", "warning",
                    CancellationToken.None);

                // Obtener logs del container fallido
                string containerLogs = await _dockerRunService.GetContainerLogsAsync(request.ContainerName, tail: 20, ct);
                if (!string.IsNullOrWhiteSpace(containerLogs))
                {
                    await _broadcaster.BroadcastLogAsync(request.DeployId,
                        $"Últimos logs del container:\n{containerLogs}", "error",
                        CancellationToken.None);
                }

                // Limpiar container fallido
                await _dockerRunService.RemoveContainerAsync(request.ContainerName, request.DeployId, CancellationToken.None);

                // Intentar auto-rollback
                await TryAutoRollbackAsync(request, deployRecord, ct);
                return;
            }

            // ─── PASO 4: Completado ───
            stopwatch.Stop();

            _store.UpdateDeploy(request.DeployId, d =>
            {
                d.Status = DeployStatus.Running;
                d.CompletedAt = DateTimeOffset.UtcNow;
            });

            _store.AddLog(request.DeployId, new DeployLog
            {
                DeployRecordId = request.DeployId,
                Message = $"Deploy completado exitosamente en {stopwatch.Elapsed.TotalSeconds:F1}s. Container: {request.ContainerName} (ID: {containerId})",
                Level = "info"
            });

            await _broadcaster.BroadcastLogAsync(request.DeployId,
                $"Deploy completado exitosamente en {stopwatch.Elapsed.TotalSeconds:F1}s. Container: {request.ContainerName}",
                cancellationToken: CancellationToken.None);

            _logger.LogInformation(
                "Deploy completado {DeployId} en {Duration}ms. Container: {ContainerName}",
                request.DeployId, stopwatch.ElapsedMilliseconds, request.ContainerName);
        }
        catch (OperationCanceledException) when (deployCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            string message = $"Timeout al procesar deploy (>{_deploySettings.TimeoutMinutes} min)";

            _store.UpdateDeploy(request.DeployId, d =>
            {
                d.Status = DeployStatus.Failed;
                d.CompletedAt = DateTimeOffset.UtcNow;
                d.ErrorMessage = message;
            });

            _store.AddLog(request.DeployId, new DeployLog
            {
                DeployRecordId = request.DeployId,
                Message = message,
                Level = "error"
            });

            await _broadcaster.BroadcastLogAsync(request.DeployId, message, "error", CancellationToken.None);
            _logger.LogWarning("Deploy {DeployId} timeout después de {Duration}ms", request.DeployId, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _store.UpdateDeploy(request.DeployId, d =>
            {
                d.Status = DeployStatus.Cancelled;
                d.CompletedAt = DateTimeOffset.UtcNow;
                d.ErrorMessage = "Deploy cancelado por cierre de la aplicación.";
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            string errorMessage = ex.Message;

            _store.UpdateDeploy(request.DeployId, d =>
            {
                d.Status = DeployStatus.Failed;
                d.CompletedAt = DateTimeOffset.UtcNow;
                d.ErrorMessage = errorMessage;
            });

            _store.AddLog(request.DeployId, new DeployLog
            {
                DeployRecordId = request.DeployId,
                Message = $"Deploy fallido: {errorMessage}",
                Level = "error"
            });

            await _broadcaster.BroadcastLogAsync(request.DeployId,
                $"Deploy fallido: {errorMessage}", "error", CancellationToken.None);

            _logger.LogError(ex,
                "Deploy fallido {DeployId} después de {Duration}ms",
                request.DeployId, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _deployCancellations.TryRemove(request.DeployId, out _);
            await _broadcaster.CompleteDeployAsync(request.DeployId);
        }
    }

    /// <summary>
    /// Intenta auto-rollback a la imagen anterior si el deploy actual falló.
    /// </summary>
    private async Task TryAutoRollbackAsync(DeployChannelRequest request, DeployRecord deployRecord, CancellationToken ct)
    {
        // Recargar del store para obtener PreviousImageName actualizado
        DeployRecord? current = _store.GetDeploy(request.DeployId);
        string? previousImage = current?.PreviousImageName;

        if (string.IsNullOrEmpty(previousImage))
        {
            // No hay imagen anterior — marcar como fallido
            _store.UpdateDeploy(request.DeployId, d =>
            {
                d.Status = DeployStatus.Failed;
                d.CompletedAt = DateTimeOffset.UtcNow;
                d.ErrorMessage = "Container falló al arrancar. No hay imagen anterior para rollback.";
            });

            _store.AddLog(request.DeployId, new DeployLog
            {
                DeployRecordId = request.DeployId,
                Message = "Container falló al arrancar. No hay imagen anterior para rollback automático.",
                Level = "error"
            });

            await _broadcaster.BroadcastLogAsync(request.DeployId,
                "Container falló al arrancar. No hay imagen anterior para rollback automático.", "error",
                CancellationToken.None);
            return;
        }

        // ⚠️ AUTO-ROLLBACK ⚠️
        await _broadcaster.BroadcastLogAsync(request.DeployId,
            $"Container falló al arrancar. Ejecutando rollback automático a: {previousImage}", "warning",
            CancellationToken.None);

        _logger.LogWarning(
            "Auto-rollback para deploy {DeployId}: {CurrentImage} → {PreviousImage}",
            request.DeployId, request.ImageName, previousImage);

        try
        {
            // Construir opciones de run con la imagen anterior
            DockerRunOptions rollbackOptions = BuildRunOptions(deployRecord) with
            {
                ImageName = previousImage
            };

            string rollbackCommand = $"docker {DockerRunService.BuildDockerRunArguments(rollbackOptions)}";
            await _broadcaster.BroadcastLogAsync(request.DeployId,
                $"Comando rollback: {rollbackCommand}", cancellationToken: CancellationToken.None);

            string? rollbackContainerId = await _dockerRunService.RunContainerAsync(rollbackOptions, request.DeployId, ct);

            if (rollbackContainerId is not null)
            {
                // Verificar que el rollback está corriendo
                await Task.Delay(2000, ct);
                bool isRunning = await _dockerRunService.IsContainerRunningAsync(request.ContainerName, ct);

                if (isRunning)
                {
                    _store.UpdateDeploy(request.DeployId, d =>
                    {
                        d.Status = DeployStatus.Running;
                        d.CompletedAt = DateTimeOffset.UtcNow;
                        d.IsRollback = true;
                        d.ContainerId = rollbackContainerId;
                        d.ImageName = previousImage;
                    });

                    await _broadcaster.BroadcastLogAsync(request.DeployId,
                        $"Rollback automático exitoso. Container corriendo con imagen: {previousImage}",
                        cancellationToken: CancellationToken.None);

                    _logger.LogInformation("Auto-rollback exitoso para deploy {DeployId}", request.DeployId);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante auto-rollback para deploy {DeployId}", request.DeployId);
        }

        // Rollback falló
        _store.UpdateDeploy(request.DeployId, d =>
        {
            d.Status = DeployStatus.Failed;
            d.CompletedAt = DateTimeOffset.UtcNow;
            d.ErrorMessage = $"Container falló al arrancar y el rollback automático a {previousImage} también falló.";
        });

        await _broadcaster.BroadcastLogAsync(request.DeployId,
            $"Rollback automático falló. Deploy en estado Failed.", "error",
            CancellationToken.None);
    }

    /// <summary>Construye las opciones de docker run desde un DeployRecord.</summary>
    private static DockerRunOptions BuildRunOptions(DeployRecord record)
    {
        return new DockerRunOptions
        {
            ImageName = record.ImageName,
            ContainerName = record.ContainerName,
            Detached = record.Detached,
            Interactive = record.Interactive,
            RestartPolicy = DockerRunService.RestartPolicyToString(record.RestartPolicy, record.OnFailureMaxRetries),
            Network = record.Network,
            Ports = record.Ports,
            Volumes = record.Volumes,
            EnvironmentVariables = record.EnvironmentVariables
        };
    }

    /// <summary>Actualiza el estado de un deploy en el store y loguea el cambio.</summary>
    private void UpdateDeployStatus(Guid deployId, DeployStatus newStatus)
    {
        _store.UpdateDeploy(deployId, d =>
        {
            d.Status = newStatus;
            if (newStatus != DeployStatus.Queued && d.StartedAt is null)
            {
                d.StartedAt = DateTimeOffset.UtcNow;
            }
        });

        _store.AddLog(deployId, new DeployLog
        {
            DeployRecordId = deployId,
            Message = $"Estado actualizado a: {newStatus}",
            Level = "info"
        });

        _logger.LogDebug("Deploy {DeployId} → {Status}", deployId, newStatus);
    }

    /// <summary>
    /// Extrae el nombre de la imagen desde el JSON de docker inspect.
    /// </summary>
    private static string? ExtractImageFromInspect(string inspectJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(inspectJson);
            // docker inspect retorna un array
            JsonElement root = doc.RootElement;
            JsonElement container = root.ValueKind == JsonValueKind.Array ? root[0] : root;

            if (container.TryGetProperty("Config", out JsonElement config) &&
                config.TryGetProperty("Image", out JsonElement image))
            {
                return image.GetString();
            }
        }
        catch
        {
            // Si falla el parsing, retornar null
        }
        return null;
    }
}
