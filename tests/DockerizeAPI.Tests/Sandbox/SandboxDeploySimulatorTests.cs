using DockerizeAPI.BackgroundServices;
using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Sandbox;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DockerizeAPI.Tests.Sandbox;

/// <summary>
/// Tests unitarios para SandboxDeploySimulator.
/// Verifica que el simulador ejecuta todos los pasos, actualiza el store
/// correctamente y soporta simulación de fallos.
/// </summary>
public sealed class SandboxDeploySimulatorTests
{
    private readonly DeployStore _store = new();
    private readonly IDeployLogBroadcaster _broadcaster = Substitute.For<IDeployLogBroadcaster>();
    private readonly ILogger<SandboxDeploySimulator> _logger = Substitute.For<ILogger<SandboxDeploySimulator>>();
    private readonly SandboxDeploySimulator _simulator;

    public SandboxDeploySimulatorTests()
    {
        _simulator = new SandboxDeploySimulator(_store, _broadcaster, _logger);
    }

    private (DeployRecord record, DeployChannelRequest request) CreateTestDeploy(
        bool simulateFailure = false, string? failAtStep = null)
    {
        Guid deployId = Guid.NewGuid();

        var record = new DeployRecord
        {
            Id = deployId,
            ImageName = "repos.dvhn/org/test-app:latest",
            ContainerName = "test-app-container",
            GitToken = "test-token",
            IsSandbox = true,
            Status = DeployStatus.Queued
        };

        _store.AddDeploy(record);

        var request = new DeployChannelRequest(
            deployId,
            record.ImageName,
            record.ContainerName,
            record.GitToken,
            "repos.dvhn",
            Sandbox: true,
            SimulateFailure: simulateFailure,
            FailAtStep: failAtStep);

        return (record, request);
    }

    [Fact]
    public async Task SimulateAsync_Exitoso_CompletaDeploy()
    {
        // Arrange
        (DeployRecord record, DeployChannelRequest request) = CreateTestDeploy();

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        DeployRecord? result = _store.GetDeploy(record.Id);
        Assert.NotNull(result);
        Assert.Equal(DeployStatus.Running, result.Status);
        Assert.NotNull(result.CompletedAt);
        Assert.NotNull(result.ContainerId);
        Assert.StartsWith("sandbox-", result.ContainerId);
    }

    [Fact]
    public async Task SimulateAsync_Exitoso_BroadcastLogs()
    {
        // Arrange
        (_, DeployChannelRequest request) = CreateTestDeploy();

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        await _broadcaster.Received().BroadcastLogAsync(
            request.DeployId,
            Arg.Is<string>(s => s.Contains("[SANDBOX]")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimulateAsync_FalloEnLoggin_MarcaFailed()
    {
        // Arrange
        (DeployRecord record, DeployChannelRequest request) = CreateTestDeploy(simulateFailure: true, failAtStep: "LoggingIn");

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        DeployRecord? result = _store.GetDeploy(record.Id);
        Assert.NotNull(result);
        Assert.Equal(DeployStatus.Failed, result.Status);
        Assert.Contains("[SANDBOX]", result.ErrorMessage);
        Assert.Contains("LoggingIn", result.ErrorMessage);
    }

    [Fact]
    public async Task SimulateAsync_FalloEnPulling_MarcaFailed()
    {
        // Arrange
        (DeployRecord record, DeployChannelRequest request) = CreateTestDeploy(simulateFailure: true, failAtStep: "Pulling");

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        DeployRecord? result = _store.GetDeploy(record.Id);
        Assert.NotNull(result);
        Assert.Equal(DeployStatus.Failed, result.Status);
        Assert.Contains("Pulling", result.ErrorMessage);
    }

    [Fact]
    public async Task SimulateAsync_FalloEnDeploying_MarcaFailed()
    {
        // Arrange
        (DeployRecord record, DeployChannelRequest request) = CreateTestDeploy(simulateFailure: true, failAtStep: "Deploying");

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        DeployRecord? result = _store.GetDeploy(record.Id);
        Assert.NotNull(result);
        Assert.Equal(DeployStatus.Failed, result.Status);
        Assert.Contains("Deploying", result.ErrorMessage);
    }

    [Fact]
    public async Task SimulateAsync_Cancelacion_LanzaOperationCanceled()
    {
        // Arrange
        (_, DeployChannelRequest request) = CreateTestDeploy();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _simulator.SimulateAsync(request, cts.Token));
    }
}
