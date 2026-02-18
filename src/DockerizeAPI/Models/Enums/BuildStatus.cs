namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Estados posibles del ciclo de vida de un build de imagen Docker.
/// El flujo normal es: Queued → Cloning → Building → Pushing → Completed.
/// </summary>
public enum BuildStatus
{
    /// <summary>Build encolado, esperando ser procesado por el BuildProcessorService.</summary>
    Queued = 0,

    /// <summary>Clonando el repositorio fuente desde Gitea.</summary>
    Cloning = 1,

    /// <summary>Construyendo la imagen con Buildah (incluye generación de Dockerfile, restore y publish).</summary>
    Building = 2,

    /// <summary>Subiendo la imagen construida al Container Registry de Gitea.</summary>
    Pushing = 3,

    /// <summary>Build finalizado exitosamente. La imagen está disponible en el registry.</summary>
    Completed = 4,

    /// <summary>Build fallido en alguno de los pasos. Consultar ErrorMessage para detalles.</summary>
    Failed = 5,

    /// <summary>Build cancelado por el usuario antes de completarse.</summary>
    Cancelled = 6
}
