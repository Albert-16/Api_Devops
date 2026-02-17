namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta del endpoint de health check.
/// Proporciona informacion sobre el estado de la API y recursos del sistema.
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// Estado general de la API ("Healthy" o "Degraded").
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Timestamp de la consulta en formato UTC.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Version de la API.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Indica si Buildah esta disponible en el sistema.
    /// </summary>
    public bool BuildahAvailable { get; set; }

    /// <summary>
    /// Version de Buildah instalada.
    /// </summary>
    public string? BuildahVersion { get; set; }

    /// <summary>
    /// Cantidad de builds actualmente en ejecucion.
    /// </summary>
    public int ActiveBuilds { get; set; }

    /// <summary>
    /// Cantidad maxima de builds simultaneos configurados.
    /// </summary>
    public int MaxConcurrentBuilds { get; set; }
}
