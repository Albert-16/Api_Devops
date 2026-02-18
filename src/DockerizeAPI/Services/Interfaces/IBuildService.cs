using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;

namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el servicio principal de gestión de builds.
/// Gestiona el ciclo de vida completo: creación, consulta, cancelación y retry.
/// </summary>
public interface IBuildService
{
    /// <summary>
    /// Crea un nuevo build, lo registra en el store y lo encola para procesamiento asíncrono.
    /// </summary>
    /// <param name="request">Datos del build a crear.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Respuesta con el ID y estado inicial del build.</returns>
    Task<BuildResponse> CreateBuildAsync(CreateBuildRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el estado detallado de un build por su ID, incluyendo logs.
    /// </summary>
    /// <param name="buildId">ID del build.</param>
    /// <returns>Detalle del build o null si no existe.</returns>
    BuildDetailResponse? GetBuildById(Guid buildId);

    /// <summary>
    /// Lista el historial de builds con paginación y filtros opcionales.
    /// </summary>
    /// <param name="page">Número de página (1-based).</param>
    /// <param name="pageSize">Tamaño de página (max 100).</param>
    /// <param name="status">Filtro opcional por estado.</param>
    /// <param name="branch">Filtro opcional por rama.</param>
    /// <param name="repositoryUrl">Filtro opcional por URL del repositorio.</param>
    /// <returns>Respuesta paginada con los builds.</returns>
    PagedResponse<BuildResponse> GetBuilds(
        int page = 1,
        int pageSize = 20,
        BuildStatus? status = null,
        string? branch = null,
        string? repositoryUrl = null);

    /// <summary>
    /// Cancela un build en progreso o encolado.
    /// </summary>
    /// <param name="buildId">ID del build a cancelar.</param>
    /// <returns>true si se canceló, false si no se pudo cancelar.</returns>
    bool CancelBuild(Guid buildId);

    /// <summary>
    /// Reintenta un build fallido usando la configuración original.
    /// </summary>
    /// <param name="buildId">ID del build a reintentar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Respuesta del build reintentado o null si no se puede reintentar.</returns>
    Task<BuildResponse?> RetryBuildAsync(Guid buildId, CancellationToken cancellationToken = default);
}
