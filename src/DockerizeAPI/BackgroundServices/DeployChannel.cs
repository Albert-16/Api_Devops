using System.Threading.Channels;

namespace DockerizeAPI.BackgroundServices;

/// <summary>
/// Canal de comunicación entre los endpoints HTTP y el DeployProcessorService.
/// Usa System.Threading.Channels para comunicación asíncrona productor-consumidor.
/// Los endpoints escriben al canal y el background service lee de él.
/// </summary>
public sealed class DeployChannel
{
    private readonly Channel<DeployChannelRequest> _channel;

    /// <summary>
    /// Inicializa el canal con capacidad bounded de 100 elementos.
    /// Si se llena, el productor espera (backpressure).
    /// </summary>
    public DeployChannel()
    {
        _channel = Channel.CreateBounded<DeployChannelRequest>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <summary>Writer del canal para encolar deploys.</summary>
    public ChannelWriter<DeployChannelRequest> Writer => _channel.Writer;

    /// <summary>Reader del canal para consumir deploys.</summary>
    public ChannelReader<DeployChannelRequest> Reader => _channel.Reader;
}

/// <summary>
/// Request interno para el pipeline de deploy.
/// Contiene la información mínima necesaria para procesar un deploy.
/// </summary>
/// <param name="DeployId">Identificador del deploy en el store.</param>
/// <param name="ImageName">Nombre completo de la imagen (registry/owner/name:tag).</param>
/// <param name="ContainerName">Nombre del container Docker.</param>
/// <param name="GitToken">Token de acceso para login al registry.</param>
/// <param name="RegistryUrl">URL del Container Registry.</param>
public sealed record DeployChannelRequest(
    Guid DeployId,
    string ImageName,
    string ContainerName,
    string GitToken,
    string RegistryUrl);
