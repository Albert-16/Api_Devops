namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Nivel de detalle en la salida del proceso de build.
/// Controla el formato de output de Buildah.
/// </summary>
public enum ProgressMode
{
    /// <summary>Detección automática según el contexto de ejecución.</summary>
    Auto = 0,

    /// <summary>Salida en texto plano sin formato especial.</summary>
    Plain = 1,

    /// <summary>Salida con formato TTY (progreso visual).</summary>
    Tty = 2
}
