namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Estados posibles de un proceso de build.
/// Cada build transiciona secuencialmente por estos estados durante su ciclo de vida.
/// </summary>
public enum BuildStatus
{
    /// <summary>En cola, esperando ser procesado por el Background Service.</summary>
    Queued,

    /// <summary>Clonando el repositorio desde Gitea.</summary>
    Cloning,

    /// <summary>Construyendo la imagen de contenedor con Buildah.</summary>
    Building,

    /// <summary>Subiendo la imagen al Gitea Container Registry.</summary>
    Pushing,

    /// <summary>Build finalizado exitosamente. La imagen esta disponible en el registry.</summary>
    Completed,

    /// <summary>El build fallo en alguno de los pasos anteriores.</summary>
    Failed,

    /// <summary>El build fue cancelado por el usuario.</summary>
    Cancelled
}
