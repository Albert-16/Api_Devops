using System.Collections.Concurrent;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Data;

/// <summary>
/// Almacenamiento en memoria de deploys usando ConcurrentDictionary.
/// Thread-safe para acceso concurrente desde múltiples deploys simultáneos.
/// Los datos se pierden al reiniciar la aplicación (diseño intencional para dev/uat).
/// </summary>
public sealed class DeployStore
{
    /// <summary>Diccionario concurrente de deploys indexados por ID.</summary>
    private readonly ConcurrentDictionary<Guid, DeployRecord> _deploys = new();

    /// <summary>Diccionario concurrente de logs indexados por DeployRecordId.</summary>
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<DeployLog>> _logs = new();

    /// <summary>
    /// Agrega un nuevo deploy al store.
    /// </summary>
    /// <param name="deploy">Deploy a registrar.</param>
    /// <returns>true si se agregó, false si ya existía.</returns>
    public bool AddDeploy(DeployRecord deploy)
    {
        return _deploys.TryAdd(deploy.Id, deploy);
    }

    /// <summary>
    /// Obtiene un deploy por su ID.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <returns>El deploy o null si no existe.</returns>
    public DeployRecord? GetDeploy(Guid deployId)
    {
        return _deploys.TryGetValue(deployId, out DeployRecord? deploy) ? deploy : null;
    }

    /// <summary>
    /// Actualiza un deploy existente aplicando una acción de mutación.
    /// Thread-safe: la acción se ejecuta sobre la referencia almacenada.
    /// </summary>
    /// <param name="deployId">ID del deploy a actualizar.</param>
    /// <param name="updateAction">Acción que modifica las propiedades del deploy.</param>
    /// <returns>true si se encontró y actualizó, false si no existe.</returns>
    public bool UpdateDeploy(Guid deployId, Action<DeployRecord> updateAction)
    {
        if (_deploys.TryGetValue(deployId, out DeployRecord? deploy))
        {
            updateAction(deploy);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Obtiene todos los deploys con paginación y filtros opcionales.
    /// Ordenados por fecha de creación descendente (más recientes primero).
    /// </summary>
    /// <param name="page">Número de página (1-based).</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="status">Filtro opcional por estado.</param>
    /// <param name="containerName">Filtro opcional por nombre de container.</param>
    /// <param name="imageName">Filtro opcional por nombre de imagen.</param>
    /// <returns>Tupla con la lista paginada y el total de registros.</returns>
    public (IReadOnlyList<DeployRecord> Items, int TotalCount) GetDeploys(
        int page = 1,
        int pageSize = 20,
        DeployStatus? status = null,
        string? containerName = null,
        string? imageName = null)
    {
        IEnumerable<DeployRecord> query = _deploys.Values;

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(containerName))
        {
            query = query.Where(d => d.ContainerName.Contains(containerName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(imageName))
        {
            query = query.Where(d => d.ImageName.Contains(imageName, StringComparison.OrdinalIgnoreCase));
        }

        List<DeployRecord> ordered = query
            .OrderByDescending(d => d.CreatedAt)
            .ToList();

        int totalCount = ordered.Count;
        int skip = (page - 1) * pageSize;

        List<DeployRecord> items = ordered
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    /// <summary>
    /// Busca el deploy activo más reciente por nombre de container.
    /// Un deploy activo es uno en estado Running, Deploying, Pulling o LoggingIn.
    /// </summary>
    /// <param name="containerName">Nombre del container a buscar.</param>
    /// <returns>El deploy activo o null si no existe.</returns>
    public DeployRecord? FindByContainerName(string containerName)
    {
        return _deploys.Values
            .Where(d => d.ContainerName.Equals(containerName, StringComparison.OrdinalIgnoreCase)
                && d.Status is DeployStatus.Running or DeployStatus.Deploying
                    or DeployStatus.Pulling or DeployStatus.LoggingIn)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// Agrega un log a un deploy específico.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <param name="log">Entrada de log a agregar.</param>
    public void AddLog(Guid deployId, DeployLog log)
    {
        ConcurrentBag<DeployLog> logs = _logs.GetOrAdd(deployId, _ => []);
        logs.Add(log);
    }

    /// <summary>
    /// Obtiene todos los logs de un deploy, ordenados por timestamp ascendente.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <returns>Lista de logs ordenados cronológicamente.</returns>
    public IReadOnlyList<DeployLog> GetLogs(Guid deployId)
    {
        if (_logs.TryGetValue(deployId, out ConcurrentBag<DeployLog>? logs))
        {
            return logs.OrderBy(l => l.Timestamp).ToList();
        }
        return [];
    }

    /// <summary>
    /// Retorna el número total de deploys almacenados.
    /// Útil para health checks y métricas.
    /// </summary>
    public int Count => _deploys.Count;
}
