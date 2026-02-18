namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Tipo de red disponible durante la construcción de la imagen con Buildah.
/// Controla el acceso a red del contenedor de build.
/// </summary>
public enum NetworkMode
{
    /// <summary>Red host del sistema. El build comparte la red del host.</summary>
    Host = 0,

    /// <summary>Red bridge aislada (default). El build tiene su propia red.</summary>
    Bridge = 1,

    /// <summary>Sin red. El build no tiene acceso a la red.</summary>
    None = 2
}
