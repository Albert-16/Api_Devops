namespace DockerizeAPI.Models.Enums;

/// <summary>
/// Políticas de reinicio para containers Docker.
/// Mapea directamente a los valores de --restart en docker run.
/// </summary>
public enum RestartPolicy
{
    /// <summary>No reiniciar automáticamente el container.</summary>
    No = 0,

    /// <summary>Reiniciar siempre, incluso si el container se detuvo manualmente.</summary>
    Always = 1,

    /// <summary>Reiniciar siempre excepto si el container fue detenido manualmente.</summary>
    UnlessStopped = 2,

    /// <summary>Reiniciar solo si el container terminó con un error (exit code != 0).</summary>
    OnFailure = 3
}
