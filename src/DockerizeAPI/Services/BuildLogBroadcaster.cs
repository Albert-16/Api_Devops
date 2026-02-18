using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Services;

/// <summary>
/// Implementación de IBuildLogBroadcaster usando Channel&lt;string&gt; por cada build.
/// Permite broadcasting de logs en tiempo real vía SSE a múltiples clientes.
/// Los channels se limpian automáticamente al completar o cancelar un build.
/// </summary>
public sealed class BuildLogBroadcaster : IBuildLogBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _channels = new();
    private readonly ILogger<BuildLogBroadcaster> _logger;

    /// <summary>Inicializa el broadcaster con el logger.</summary>
    public BuildLogBroadcaster(ILogger<BuildLogBroadcaster> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastLogAsync(Guid buildId, string message, string level = "info", CancellationToken cancellationToken = default)
    {
        Channel<string> channel = GetOrCreateChannel(buildId);

        var logEntry = new
        {
            timestamp = DateTimeOffset.UtcNow.ToString("o"),
            level,
            message
        };

        string json = JsonSerializer.Serialize(logEntry);

        try
        {
            await channel.Writer.WriteAsync(json, cancellationToken);
        }
        catch (ChannelClosedException)
        {
            _logger.LogDebug("Canal cerrado para build {BuildId}, log descartado", buildId);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> SubscribeAsync(
        Guid buildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<string> channel = GetOrCreateChannel(buildId);

        await foreach (string logLine in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return logLine;
        }
    }

    /// <inheritdoc/>
    public Task CompleteBuildAsync(Guid buildId)
    {
        if (_channels.TryRemove(buildId, out Channel<string>? channel))
        {
            channel.Writer.TryComplete();
            _logger.LogDebug("Canal de logs completado y removido para build {BuildId}", buildId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Obtiene un channel existente o crea uno nuevo para el buildId.
    /// Los channels son bounded con capacidad de 1000 mensajes para prevenir memory leaks.
    /// </summary>
    private Channel<string> GetOrCreateChannel(Guid buildId)
    {
        return _channels.GetOrAdd(buildId, _ =>
        {
            _logger.LogDebug("Creando canal de logs para build {BuildId}", buildId);
            return Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });
        });
    }
}
