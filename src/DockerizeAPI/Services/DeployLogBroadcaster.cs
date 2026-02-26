using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Services;

/// <summary>
/// Implementación de IDeployLogBroadcaster usando Channel&lt;string&gt; por cada deploy.
/// Permite broadcasting de logs en tiempo real vía SSE a múltiples clientes.
/// Los channels se limpian automáticamente al completar o cancelar un deploy.
/// </summary>
public sealed class DeployLogBroadcaster : IDeployLogBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _channels = new();
    private readonly ILogger<DeployLogBroadcaster> _logger;

    /// <summary>Inicializa el broadcaster con el logger.</summary>
    public DeployLogBroadcaster(ILogger<DeployLogBroadcaster> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastLogAsync(Guid deployId, string message, string level = "info", CancellationToken cancellationToken = default)
    {
        Channel<string> channel = GetOrCreateChannel(deployId);

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
            _logger.LogDebug("Canal cerrado para deploy {DeployId}, log descartado", deployId);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> SubscribeAsync(
        Guid deployId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<string> channel = GetOrCreateChannel(deployId);

        await foreach (string logLine in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return logLine;
        }
    }

    /// <inheritdoc/>
    public Task CompleteDeployAsync(Guid deployId)
    {
        if (_channels.TryRemove(deployId, out Channel<string>? channel))
        {
            channel.Writer.TryComplete();
            _logger.LogDebug("Canal de logs completado y removido para deploy {DeployId}", deployId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Obtiene un channel existente o crea uno nuevo para el deployId.
    /// Los channels son bounded con capacidad de 1000 mensajes para prevenir memory leaks.
    /// </summary>
    private Channel<string> GetOrCreateChannel(Guid deployId)
    {
        return _channels.GetOrAdd(deployId, _ =>
        {
            _logger.LogDebug("Creando canal de logs para deploy {DeployId}", deployId);
            return Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });
        });
    }
}
