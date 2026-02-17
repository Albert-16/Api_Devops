namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta estandar de error para la API.
/// Proporciona un formato consistente para todos los errores.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Mensaje descriptivo del error.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detalles adicionales del error (opcional).
    /// Puede contener informacion especifica como errores de validacion.
    /// </summary>
    public object? Details { get; set; }

    /// <summary>
    /// Timestamp del error en formato UTC.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
