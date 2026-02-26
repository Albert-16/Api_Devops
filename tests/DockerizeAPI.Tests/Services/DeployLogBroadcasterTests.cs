using System.Text.Json;
using DockerizeAPI.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para DeployLogBroadcaster.
/// Verifica broadcasting de logs y gestión de canales por deploy.
/// </summary>
public sealed class DeployLogBroadcasterTests
{
    private readonly DeployLogBroadcaster _broadcaster;

    public DeployLogBroadcasterTests()
    {
        ILogger<DeployLogBroadcaster> logger = Substitute.For<ILogger<DeployLogBroadcaster>>();
        _broadcaster = new DeployLogBroadcaster(logger);
    }

    [Fact]
    public async Task BroadcastLogAsync_EmiteLogEnFormatoJson()
    {
        // Arrange
        Guid deployId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await _broadcaster.BroadcastLogAsync(deployId, "Test message", "info", cts.Token);

        // Leer el mensaje emitido
        string? received = null;
        await foreach (string log in _broadcaster.SubscribeAsync(deployId, cts.Token))
        {
            received = log;
            break;
        }

        // Assert
        Assert.NotNull(received);
        using JsonDocument doc = JsonDocument.Parse(received);
        Assert.Equal("Test message", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal("info", doc.RootElement.GetProperty("level").GetString());
        Assert.True(doc.RootElement.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task CompleteDeployAsync_CierraCanalCorrectamente()
    {
        // Arrange
        Guid deployId = Guid.NewGuid();
        var messages = new List<string>();

        await _broadcaster.BroadcastLogAsync(deployId, "Message 1");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        Task readTask = Task.Run(async () =>
        {
            await foreach (string log in _broadcaster.SubscribeAsync(deployId, cts.Token))
            {
                messages.Add(log);
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);
        await _broadcaster.CompleteDeployAsync(deployId);

        // Act — el readTask debería terminar porque el canal fue completado
        await readTask;

        // Assert
        Assert.Single(messages);
        Assert.Contains("Message 1", messages[0]);
    }

    [Fact]
    public async Task BroadcastLogAsync_MultipleDeploys_AisladosCorrectamente()
    {
        // Arrange
        Guid deployId1 = Guid.NewGuid();
        Guid deployId2 = Guid.NewGuid();

        // Act
        await _broadcaster.BroadcastLogAsync(deployId1, "Deploy 1 message");
        await _broadcaster.BroadcastLogAsync(deployId2, "Deploy 2 message");

        // Assert — cada deploy tiene su canal
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        string? msg1 = null;
        await foreach (string log in _broadcaster.SubscribeAsync(deployId1, cts.Token))
        {
            msg1 = log;
            break;
        }

        Assert.NotNull(msg1);
        Assert.Contains("Deploy 1 message", msg1);
    }
}
