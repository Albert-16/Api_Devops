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
/// Tests unitarios para SandboxBuildSimulator.
/// Verifica que el simulador ejecuta todos los pasos, actualiza el store
/// correctamente y soporta simulación de fallos.
/// </summary>
public sealed class SandboxBuildSimulatorTests
{
    private readonly BuildStore _store = new();
    private readonly IBuildLogBroadcaster _broadcaster = Substitute.For<IBuildLogBroadcaster>();
    private readonly IDockerfileGenerator _dockerfileGenerator = Substitute.For<IDockerfileGenerator>();
    private readonly ILogger<SandboxBuildSimulator> _logger = Substitute.For<ILogger<SandboxBuildSimulator>>();
    private readonly SandboxBuildSimulator _simulator;

    public SandboxBuildSimulatorTests()
    {
        _dockerfileGenerator.Generate(Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns("FROM mcr.microsoft.com/dotnet/aspnet:10.0\nWORKDIR /app");

        _simulator = new SandboxBuildSimulator(_store, _broadcaster, _dockerfileGenerator, _logger);
    }

    private (BuildRecord record, BuildChannelRequest request) CreateTestBuild(
        bool simulateFailure = false, string? failAtStep = null)
    {
        Guid buildId = Guid.NewGuid();

        var record = new BuildRecord
        {
            Id = buildId,
            RepositoryUrl = "https://repos.dvhn/org/test-repo.git",
            Branch = "main",
            GitToken = "test-token",
            ImageName = "test-app",
            ImageTag = "latest",
            IsSandbox = true,
            Status = BuildStatus.Queued
        };

        _store.AddBuild(record);

        var request = new BuildChannelRequest(
            buildId,
            record.RepositoryUrl,
            record.Branch,
            record.GitToken,
            record.ImageName,
            record.ImageTag,
            false,
            "repos.dvhn",
            "org",
            Sandbox: true,
            SimulateFailure: simulateFailure,
            FailAtStep: failAtStep);

        return (record, request);
    }

    [Fact]
    public async Task SimulateAsync_Exitoso_CompletaBuild()
    {
        // Arrange
        (BuildRecord record, BuildChannelRequest request) = CreateTestBuild();

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        BuildRecord? result = _store.GetBuild(record.Id);
        Assert.NotNull(result);
        Assert.Equal(BuildStatus.Completed, result.Status);
        Assert.NotNull(result.CompletedAt);
        Assert.NotNull(result.ImageUrl);
        Assert.NotNull(result.GeneratedDockerfile);
        Assert.NotNull(result.CsprojPath);
        Assert.NotNull(result.AssemblyName);
        Assert.NotNull(result.CommitSha);
    }

    [Fact]
    public async Task SimulateAsync_Exitoso_BroadcastLogs()
    {
        // Arrange
        (_, BuildChannelRequest request) = CreateTestBuild();

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert — debe haber broadcast múltiples logs (al menos primer y último con [SANDBOX])
        await _broadcaster.Received().BroadcastLogAsync(
            request.BuildId,
            Arg.Is<string>(s => s.Contains("[SANDBOX]")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimulateAsync_FalloEnCloning_MarcaFailed()
    {
        // Arrange
        (BuildRecord record, BuildChannelRequest request) = CreateTestBuild(simulateFailure: true, failAtStep: "Cloning");

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        BuildRecord? result = _store.GetBuild(record.Id);
        Assert.NotNull(result);
        Assert.Equal(BuildStatus.Failed, result.Status);
        Assert.Contains("[SANDBOX]", result.ErrorMessage);
        Assert.Contains("Cloning", result.ErrorMessage);
    }

    [Fact]
    public async Task SimulateAsync_FalloEnBuilding_MarcaFailed()
    {
        // Arrange
        (BuildRecord record, BuildChannelRequest request) = CreateTestBuild(simulateFailure: true, failAtStep: "Building");

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        BuildRecord? result = _store.GetBuild(record.Id);
        Assert.NotNull(result);
        Assert.Equal(BuildStatus.Failed, result.Status);
        Assert.Contains("Building", result.ErrorMessage);
    }

    [Fact]
    public async Task SimulateAsync_FalloEnPushing_MarcaFailed()
    {
        // Arrange
        (BuildRecord record, BuildChannelRequest request) = CreateTestBuild(simulateFailure: true, failAtStep: "Pushing");

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert
        BuildRecord? result = _store.GetBuild(record.Id);
        Assert.NotNull(result);
        Assert.Equal(BuildStatus.Failed, result.Status);
        Assert.Contains("Pushing", result.ErrorMessage);
    }

    [Fact]
    public async Task SimulateAsync_Cancelacion_LanzaOperationCanceled()
    {
        // Arrange
        (_, BuildChannelRequest request) = CreateTestBuild();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _simulator.SimulateAsync(request, cts.Token));
    }

    [Fact]
    public async Task SimulateAsync_GeneraDockerfileReal()
    {
        // Arrange
        (BuildRecord record, BuildChannelRequest request) = CreateTestBuild();

        // Act
        await _simulator.SimulateAsync(request, CancellationToken.None);

        // Assert — verifica que se llamó al generador real
        _dockerfileGenerator.Received(1).Generate(
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<string?>());

        BuildRecord? result = _store.GetBuild(record.Id);
        Assert.NotNull(result);
        Assert.Equal("FROM mcr.microsoft.com/dotnet/aspnet:10.0\nWORKDIR /app", result.GeneratedDockerfile);
    }
}
