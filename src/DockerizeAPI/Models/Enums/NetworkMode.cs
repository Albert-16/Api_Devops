namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Tipo de red que se usa durante la construccion de la imagen con Buildah.
/// Controla el acceso a la red que tiene el proceso de build.
/// </summary>
public enum NetworkMode
{
    /// <summary>Usa la red del host directamente. Util cuando el build necesita acceder a servicios locales.</summary>
    Host,

    /// <summary>Red aislada tipo bridge (por defecto). Provee aislamiento de red basico.</summary>
    Bridge,

    /// <summary>Sin acceso a red. Util para builds que no necesitan descargar nada.</summary>
    None
}
