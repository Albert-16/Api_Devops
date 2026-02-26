using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;

namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el servicio principal de gestión de deploys.
/// Gestiona el ciclo de vida completo: creación, consulta, gestión de containers y rollback.
/// </summary>
public interface IDeployService
{
    /// <summary>
    /// Crea un nuevo deploy, lo registra en el store y lo encola para procesamiento asíncrono.
    /// </summary>
    /// <param name="request">Datos del deploy a crear.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Respuesta con el ID y estado inicial del deploy.</returns>
    Task<DeployResponse> CreateDeployAsync(CreateDeployRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el estado detallado de un deploy por su ID, incluyendo logs.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <returns>Detalle del deploy o null si no existe.</returns>
    DeployDetailResponse? GetDeployById(Guid deployId);

    /// <summary>
    /// Lista el historial de deploys con paginación y filtros opcionales.
    /// </summary>
    /// <param name="page">Número de página (1-based).</param>
    /// <param name="pageSize">Tamaño de página (max 100).</param>
    /// <param name="status">Filtro opcional por estado.</param>
    /// <param name="containerName">Filtro opcional por nombre de container.</param>
    /// <param name="imageName">Filtro opcional por nombre de imagen.</param>
    /// <returns>Respuesta paginada con los deploys.</returns>
    PagedResponse<DeployResponse> GetDeploys(
        int page = 1,
        int pageSize = 20,
        DeployStatus? status = null,
        string? containerName = null,
        string? imageName = null);

    /// <summary>
    /// Detiene un container asociado a un deploy.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si se detuvo exitosamente.</returns>
    Task<bool> StopDeployAsync(Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reinicia un container asociado a un deploy.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si se reinició exitosamente.</returns>
    Task<bool> RestartDeployAsync(Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene y elimina un container asociado a un deploy.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si se eliminó exitosamente.</returns>
    Task<bool> RemoveDeployAsync(Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la información de docker inspect de un container.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Respuesta con el JSON de inspect o null si no existe.</returns>
    Task<ContainerInspectResponse?> InspectDeployAsync(Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta rollback a la imagen anterior para un deploy.
    /// </summary>
    /// <param name="deployId">ID del deploy a revertir.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Respuesta del nuevo deploy de rollback o null si no es posible.</returns>
    Task<DeployResponse?> RollbackDeployAsync(Guid deployId, CancellationToken cancellationToken = default);
}
