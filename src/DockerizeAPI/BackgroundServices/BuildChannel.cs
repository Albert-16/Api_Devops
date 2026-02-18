using System.Threading.Channels;

namespace DockerizeAPI.BackgroundServices;

/// <summary>
/// Canal de comunicación entre los endpoints HTTP y el BuildProcessorService.
/// Usa System.Threading.Channels para comunicación asíncrona productor-consumidor.
/// Los endpoints escriben al canal y el background service lee de él.
/// </summary>
public sealed class BuildChannel
{
    private readonly Channel<BuildChannelRequest> _channel;

    /// <summary>
    /// Inicializa el canal con capacidad bounded de 100 elementos.
    /// Si se llena, el productor espera (backpressure).
    /// </summary>
    public BuildChannel()
    {
        _channel = Channel.CreateBounded<BuildChannelRequest>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <summary>Writer del canal para encolar builds.</summary>
    public ChannelWriter<BuildChannelRequest> Writer => _channel.Writer;

    /// <summary>Reader del canal para consumir builds.</summary>
    public ChannelReader<BuildChannelRequest> Reader => _channel.Reader;
}

/// <summary>
/// Request interno para el pipeline de build.
/// Contiene toda la información necesaria para procesar un build.
/// </summary>
/// <param name="BuildId">Identificador del build en el store.</param>
/// <param name="RepositoryUrl">URL del repositorio a clonar.</param>
/// <param name="Branch">Rama a construir.</param>
/// <param name="GitToken">Token de acceso (para clone, login y push).</param>
/// <param name="ImageName">Nombre de la imagen sin tag.</param>
/// <param name="ImageTag">Tag de la imagen.</param>
/// <param name="IncludeOdbcDependencies">Si es true, usa template ODBC/Debian.</param>
/// <param name="RegistryUrl">URL del Container Registry.</param>
/// <param name="RegistryOwner">Owner/organización del registry.</param>
public sealed record BuildChannelRequest(
    Guid BuildId,
    string RepositoryUrl,
    string Branch,
    string GitToken,
    string ImageName,
    string ImageTag,
    bool IncludeOdbcDependencies,
    string RegistryUrl,
    string RegistryOwner);
