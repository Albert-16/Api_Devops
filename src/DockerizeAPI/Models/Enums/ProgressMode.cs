namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Nivel de detalle en la salida del proceso de build con Buildah.
/// Controla cuanta informacion se muestra durante la construccion.
/// </summary>
public enum ProgressMode
{
    /// <summary>Deteccion automatica del nivel de detalle segun el entorno.</summary>
    Auto,

    /// <summary>Salida plana sin formato especial. Ideal para logs y CI/CD.</summary>
    Plain,

    /// <summary>Salida formateada para terminal interactiva con indicadores de progreso.</summary>
    Tty
}
