using System.Diagnostics;
using DockerizeAPI.BackgroundServices;
using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Sandbox;

/// <summary>
/// Simulador de pipeline de deploy para modo sandbox.
/// Ejecuta todos los pasos del deploy con logs realistas y delays simulados,
/// sin ejecutar operaciones reales de docker.
/// </summary>
public sealed class SandboxDeploySimulator
{
    private readonly DeployStore _store;
    private readonly IDeployLogBroadcaster _broadcaster;
    private readonly ILogger<SandboxDeploySimulator> _logger;

    /// <summary>Delay entre líneas de log para simular latencia real.</summary>
    private const int LoginDelayMs = 500;
    private const int PullLineDelayMs = 300;
    private const int RunLineDelayMs = 500;
    private const int VerifyDelayMs = 2000;

    public SandboxDeploySimulator(
        DeployStore store,
        IDeployLogBroadcaster broadcaster,
        ILogger<SandboxDeploySimulator> logger)
    {
        _store = store;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Simula el pipeline completo de deploy con logs realistas.
    /// </summary>
    public async Task SimulateAsync(DeployChannelRequest request, CancellationToken ct)
    {
        string containerId = $"sandbox-{Guid.NewGuid():N}"[..20];
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[SANDBOX] Iniciando deploy simulado {DeployId}: {ImageName} → {ContainerName}",
            request.DeployId, request.ImageName, request.ContainerName);

        await BroadcastAsync(request.DeployId, "[SANDBOX] Iniciando deploy en modo sandbox — sin operaciones reales", ct);

        // ─── PASO 1: Login al registry (simulado) ───
        UpdateDeployStatus(request.DeployId, DeployStatus.LoggingIn);

        if (ShouldFailAtStep(request, "loggingin"))
        {
            await SimulateFailureAsync(request.DeployId, "LoggingIn", ct);
            return;
        }

        foreach (string log in SandboxLogTemplates.GetLoginLogs(request.RegistryUrl))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.DeployId, log, ct);
            await Task.Delay(LoginDelayMs, ct);
        }

        // ─── PASO 2: Pull de imagen (simulado) ───
        UpdateDeployStatus(request.DeployId, DeployStatus.Pulling);

        if (ShouldFailAtStep(request, "pulling"))
        {
            await SimulateFailureAsync(request.DeployId, "Pulling", ct);
            return;
        }

        foreach (string log in SandboxLogTemplates.GetPullLogs(request.ImageName))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.DeployId, log, ct);
            await Task.Delay(PullLineDelayMs, ct);
        }

        // ─── PASO 3: Docker run (simulado) ───
        UpdateDeployStatus(request.DeployId, DeployStatus.Deploying);

        if (ShouldFailAtStep(request, "deploying"))
        {
            await SimulateFailureAsync(request.DeployId, "Deploying", ct);
            return;
        }

        // Simular log del comando docker run (como hace el pipeline real)
        string simulatedCommand = $"docker run -d --name {request.ContainerName} --restart always {request.ImageName}";
        await BroadcastAsync(request.DeployId, $"Comando: {simulatedCommand}", ct);

        foreach (string log in SandboxLogTemplates.GetDockerRunLogs(request.ContainerName, containerId))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.DeployId, log, ct);
            await Task.Delay(RunLineDelayMs, ct);
        }

        // Verificar (simulado)
        await BroadcastAsync(request.DeployId, "Verificando que el container está corriendo...", ct);
        await Task.Delay(VerifyDelayMs, ct);

        // ─── PASO 4: Completar ───
        stopwatch.Stop();

        _store.UpdateDeploy(request.DeployId, d =>
        {
            d.Status = DeployStatus.Running;
            d.CompletedAt = DateTimeOffset.UtcNow;
            d.ContainerId = containerId;
        });

        string completionMessage = $"Deploy completado exitosamente en {stopwatch.Elapsed.TotalSeconds:F1}s. Container: {request.ContainerName} (ID: {containerId})";

        _store.AddLog(request.DeployId, new DeployLog
        {
            DeployRecordId = request.DeployId,
            Message = completionMessage,
            Level = "info"
        });

        await BroadcastAsync(request.DeployId, completionMessage, ct);
        await BroadcastAsync(request.DeployId,
            $"[SANDBOX] Deploy simulado completado. Container: {request.ContainerName}", ct);

        _logger.LogInformation("[SANDBOX] Deploy simulado completado {DeployId} en {Duration}ms",
            request.DeployId, stopwatch.ElapsedMilliseconds);
    }

    private static bool ShouldFailAtStep(DeployChannelRequest request, string step)
    {
        return request.SimulateFailure &&
               string.Equals(request.FailAtStep, step, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SimulateFailureAsync(Guid deployId, string step, CancellationToken ct)
    {
        foreach (string log in SandboxLogTemplates.GetFailureLogs(step))
        {
            await BroadcastAsync(deployId, log, "error", ct);
        }

        _store.UpdateDeploy(deployId, d =>
        {
            d.Status = DeployStatus.Failed;
            d.CompletedAt = DateTimeOffset.UtcNow;
            d.ErrorMessage = $"[SANDBOX] Fallo simulado en paso: {step}";
        });

        _store.AddLog(deployId, new DeployLog
        {
            DeployRecordId = deployId,
            Message = $"[SANDBOX] Fallo simulado en paso: {step}",
            Level = "error"
        });

        _logger.LogInformation("[SANDBOX] Deploy {DeployId} fallo simulado en paso: {Step}", deployId, step);
    }

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
    }

    private async Task BroadcastAsync(Guid deployId, string message, CancellationToken ct)
    {
        await _broadcaster.BroadcastLogAsync(deployId, message, "info", ct);
    }

    private async Task BroadcastAsync(Guid deployId, string message, string level, CancellationToken ct)
    {
        await _broadcaster.BroadcastLogAsync(deployId, message, level, ct);
    }
}
