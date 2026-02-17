using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta al crear o consultar un build.
/// Contiene el estado actual, timestamps de cada paso y la URL de la imagen si se completo.
/// </summary>
public class BuildResponse
{
    /// <summary>
    /// Identificador unico del build (GUID).
    /// </summary>
    public string BuildId { get; set; } = string.Empty;

    /// <summary>
    /// Estado actual del build.
    /// </summary>
    public BuildStatus Status { get; set; }

    /// <summary>
    /// URL del repositorio que se esta construyendo.
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Rama del repositorio.
    /// </summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>
    /// URL completa de la imagen en el Container Registry.
    /// Solo disponible cuando el status es Completed.
    /// Ejemplo: "repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web:sapp-dev"
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Si se incluyeron dependencias ODBC (template Debian) o no (template Alpine).
    /// </summary>
    public bool IncludeOdbcDependencies { get; set; }

    /// <summary>
    /// Fecha y hora de creacion del build.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha y hora de inicio de la clonacion del repositorio.
    /// </summary>
    public DateTime? CloningStartedAt { get; set; }

    /// <summary>
    /// Fecha y hora de inicio de la construccion de la imagen.
    /// </summary>
    public DateTime? BuildingStartedAt { get; set; }

    /// <summary>
    /// Fecha y hora de inicio de la subida al registry.
    /// </summary>
    public DateTime? PushingStartedAt { get; set; }

    /// <summary>
    /// Fecha y hora de finalizacion del build (exito o fallo).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Mensaje de error si el build fallo. Null si fue exitoso.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Ultimas lineas de log del build (resumen).
    /// Para logs completos, usar el endpoint GET /api/builds/{buildId}/logs.
    /// </summary>
    public List<string> RecentLogs { get; set; } = [];
}
