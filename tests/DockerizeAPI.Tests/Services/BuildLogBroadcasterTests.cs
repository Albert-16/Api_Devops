using System.Text.Json;
using DockerizeAPI.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para BuildLogBroadcaster.
/// Verifica broadcasting de logs y gestión de canales por build.
/// </summary>
public sealed class BuildLogBroadcasterTests
{
    private readonly BuildLogBroadcaster _broadcaster;

    public BuildLogBroadcasterTests()
    {
        ILogger<BuildLogBroadcaster> logger = Substitute.For<ILogger<BuildLogBroadcaster>>();
        _broadcaster = new BuildLogBroadcaster(logger);
    }

    [Fact]
    public async Task BroadcastLogAsync_EmiteLogEnFormatoJson()
    {
        // Arrange
        Guid buildId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await _broadcaster.BroadcastLogAsync(buildId, "Test message", "info", cts.Token);

        // Leer el mensaje emitido
        string? received = null;
        await foreach (string log in _broadcaster.SubscribeAsync(buildId, cts.Token))
        {
            received = log;
            break; // Solo necesitamos el primero
        }

        // Assert
        Assert.NotNull(received);
        using JsonDocument doc = JsonDocument.Parse(received);
        Assert.Equal("Test message", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal("info", doc.RootElement.GetProperty("level").GetString());
        Assert.True(doc.RootElement.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task CompleteBuildAsync_CierraCanalCorrectamente()
    {
        // Arrange
        Guid buildId = Guid.NewGuid();
        var messages = new List<string>();

        // Suscribirse ANTES de completar para leer del canal existente
        await _broadcaster.BroadcastLogAsync(buildId, "Message 1");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Leer el mensaje existente y luego completar el build en paralelo
        Task readTask = Task.Run(async () =>
        {
            await foreach (string log in _broadcaster.SubscribeAsync(buildId, cts.Token))
            {
                messages.Add(log);
            }
        }, cts.Token);

        // Esperar un momento para que la suscripción inicie, luego completar
        await Task.Delay(100, cts.Token);
        await _broadcaster.CompleteBuildAsync(buildId);

        // Act — el readTask debería terminar porque el canal fue completado
        await readTask;

        // Assert
        Assert.Single(messages);
        Assert.Contains("Message 1", messages[0]);
    }

    [Fact]
    public async Task BroadcastLogAsync_MultiplesBuilds_AisladosCorrectamente()
    {
        // Arrange
        Guid buildId1 = Guid.NewGuid();
        Guid buildId2 = Guid.NewGuid();

        // Act
        await _broadcaster.BroadcastLogAsync(buildId1, "Build 1 message");
        await _broadcaster.BroadcastLogAsync(buildId2, "Build 2 message");

        // Assert — cada build tiene su canal
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        string? msg1 = null;
        await foreach (string log in _broadcaster.SubscribeAsync(buildId1, cts.Token))
        {
            msg1 = log;
            break;
        }

        Assert.NotNull(msg1);
        Assert.Contains("Build 1 message", msg1);
    }
}
