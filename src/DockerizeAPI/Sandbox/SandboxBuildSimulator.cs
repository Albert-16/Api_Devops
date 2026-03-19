using System.Diagnostics;
using DockerizeAPI.BackgroundServices;
using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Sandbox;

/// <summary>
/// Simulador de pipeline de build para modo sandbox.
/// Ejecuta todos los pasos del build con logs realistas y delays simulados,
/// sin ejecutar operaciones reales de git o docker.
/// </summary>
public sealed class SandboxBuildSimulator
{
    private readonly BuildStore _store;
    private readonly IBuildLogBroadcaster _broadcaster;
    private readonly IDockerfileGenerator _dockerfileGenerator;
    private readonly ILogger<SandboxBuildSimulator> _logger;

    /// <summary>Delay entre líneas de log para simular latencia real.</summary>
    private const int CloneLineDelayMs = 200;
    private const int DetectDelayMs = 300;
    private const int SharedFilesLineDelayMs = 200;
    private const int BuildStepDelayMs = 300;
    private const int PushLineDelayMs = 400;

    public SandboxBuildSimulator(
        BuildStore store,
        IBuildLogBroadcaster broadcaster,
        IDockerfileGenerator dockerfileGenerator,
        ILogger<SandboxBuildSimulator> logger)
    {
        _store = store;
        _broadcaster = broadcaster;
        _dockerfileGenerator = dockerfileGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Simula el pipeline completo de build con logs realistas.
    /// </summary>
    public async Task SimulateAsync(BuildChannelRequest request, CancellationToken ct)
    {
        string fullImageTag = $"{request.RegistryUrl}/{request.RegistryOwner}/{request.ImageName}:{request.ImageTag}";
        string simulatedCsprojPath = $"src/{request.ImageName}/{request.ImageName}.csproj";
        string simulatedAssemblyName = request.ImageName;

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[SANDBOX] Iniciando build simulado {BuildId}: {ImageTag}", request.BuildId, fullImageTag);

        await BroadcastAsync(request.BuildId, "[SANDBOX] Iniciando build en modo sandbox — sin operaciones reales", ct);

        // ─── PASO 1: Clonar repositorio (simulado) ───
        UpdateBuildStatus(request.BuildId, BuildStatus.Cloning);

        if (ShouldFailAtStep(request, "cloning"))
        {
            await SimulateFailureAsync(request.BuildId, "Cloning", ct);
            return;
        }

        foreach (string log in SandboxLogTemplates.GetCloneLogs(request.RepositoryUrl, request.Branch))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.BuildId, log, ct);
            await Task.Delay(CloneLineDelayMs, ct);
        }

        string simulatedCommitSha = $"sandbox{Guid.NewGuid():N}"[..12];
        _store.UpdateBuild(request.BuildId, b => b.CommitSha = simulatedCommitSha);

        // ─── PASO 2: Detectar .csproj (simulado) ───
        foreach (string log in SandboxLogTemplates.GetDetectCsprojLogs(simulatedCsprojPath, simulatedAssemblyName))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.BuildId, log, ct);
            await Task.Delay(DetectDelayMs, ct);
        }

        _store.UpdateBuild(request.BuildId, b =>
        {
            b.CsprojPath = simulatedCsprojPath;
            b.AssemblyName = simulatedAssemblyName;
        });

        // ─── PASO 2.5: Shared files (simulado) ───
        foreach (string log in SandboxLogTemplates.GetSharedFilesLogs(request.IncludeOdbcDependencies))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.BuildId, log, ct);
            await Task.Delay(SharedFilesLineDelayMs, ct);
        }

        // ─── PASO 3: Generar Dockerfile (real — sin I/O) ───
        UpdateBuildStatus(request.BuildId, BuildStatus.Building);

        if (ShouldFailAtStep(request, "building"))
        {
            await SimulateFailureAsync(request.BuildId, "Building", ct);
            return;
        }

        string dockerfile = _dockerfileGenerator.Generate(
            request.IncludeOdbcDependencies,
            simulatedCsprojPath,
            simulatedAssemblyName);

        _store.UpdateBuild(request.BuildId, b => b.GeneratedDockerfile = dockerfile);

        // ─── PASO 4: Docker build (simulado) ───
        foreach (string log in SandboxLogTemplates.GetDockerBuildLogs(fullImageTag, request.IncludeOdbcDependencies))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.BuildId, log, ct);
            await Task.Delay(BuildStepDelayMs, ct);
        }

        // ─── PASO 5: Push (simulado) ───
        UpdateBuildStatus(request.BuildId, BuildStatus.Pushing);

        if (ShouldFailAtStep(request, "pushing"))
        {
            await SimulateFailureAsync(request.BuildId, "Pushing", ct);
            return;
        }

        foreach (string log in SandboxLogTemplates.GetPushLogs(fullImageTag))
        {
            ct.ThrowIfCancellationRequested();
            await BroadcastAsync(request.BuildId, log, ct);
            await Task.Delay(PushLineDelayMs, ct);
        }

        // ─── PASO 6: Completar ───
        stopwatch.Stop();

        _store.UpdateBuild(request.BuildId, b =>
        {
            b.Status = BuildStatus.Completed;
            b.CompletedAt = DateTimeOffset.UtcNow;
            b.ImageUrl = fullImageTag;
        });

        string completionMessage = $"Build completado exitosamente en {stopwatch.Elapsed.TotalSeconds:F1}s. Imagen: {fullImageTag}";

        _store.AddLog(request.BuildId, new BuildLog
        {
            BuildRecordId = request.BuildId,
            Message = completionMessage,
            Level = "info"
        });

        await BroadcastAsync(request.BuildId, completionMessage, ct);
        await BroadcastAsync(request.BuildId,
            $"[SANDBOX] Build simulado completado. Imagen: {fullImageTag}", ct);

        _logger.LogInformation("[SANDBOX] Build simulado completado {BuildId} en {Duration}ms",
            request.BuildId, stopwatch.ElapsedMilliseconds);
    }

    private static bool ShouldFailAtStep(BuildChannelRequest request, string step)
    {
        return request.SimulateFailure &&
               string.Equals(request.FailAtStep, step, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SimulateFailureAsync(Guid buildId, string step, CancellationToken ct)
    {
        foreach (string log in SandboxLogTemplates.GetFailureLogs(step))
        {
            await BroadcastAsync(buildId, log, "error", ct);
        }

        _store.UpdateBuild(buildId, b =>
        {
            b.Status = BuildStatus.Failed;
            b.CompletedAt = DateTimeOffset.UtcNow;
            b.ErrorMessage = $"[SANDBOX] Fallo simulado en paso: {step}";
        });

        _store.AddLog(buildId, new BuildLog
        {
            BuildRecordId = buildId,
            Message = $"[SANDBOX] Fallo simulado en paso: {step}",
            Level = "error"
        });

        _logger.LogInformation("[SANDBOX] Build {BuildId} fallo simulado en paso: {Step}", buildId, step);
    }

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
    }

    private async Task BroadcastAsync(Guid buildId, string message, CancellationToken ct)
    {
        await _broadcaster.BroadcastLogAsync(buildId, message, "info", ct);
    }

    private async Task BroadcastAsync(Guid buildId, string message, string level, CancellationToken ct)
    {
        await _broadcaster.BroadcastLogAsync(buildId, message, level, ct);
    }
}
