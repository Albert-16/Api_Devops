namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Estados posibles del ciclo de vida de un deploy de container Docker.
/// El flujo normal es: Queued -> LoggingIn -> Pulling -> Deploying -> Running.
/// </summary>
public enum DeployStatus
{
    /// <summary>Deploy encolado, esperando ser procesado por el DeployProcessorService.</summary>
    Queued = 0,

    /// <summary>Autenticando contra el Container Registry.</summary>
    LoggingIn = 1,

    /// <summary>Descargando la imagen desde el Container Registry.</summary>
    Pulling = 2,

    /// <summary>Ejecutando docker run para crear y arrancar el container.</summary>
    Deploying = 3,

    /// <summary>Container corriendo exitosamente.</summary>
    Running = 4,

    /// <summary>Container detenido (stop manual o rollback).</summary>
    Stopped = 5,

    /// <summary>Deploy fallido en alguno de los pasos. Consultar ErrorMessage para detalles.</summary>
    Failed = 6,

    /// <summary>Deploy cancelado por el usuario antes de completarse.</summary>
    Cancelled = 7
}
