using System.Collections.Concurrent;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Data;

/// <summary>
/// Almacenamiento en memoria de builds usando ConcurrentDictionary.
/// Thread-safe para acceso concurrente desde múltiples builds simultáneos.
/// Los datos se pierden al reiniciar la aplicación (diseño intencional para dev/uat).
/// </summary>
public sealed class BuildStore
{
    /// <summary>Diccionario concurrente de builds indexados por ID.</summary>
    private readonly ConcurrentDictionary<Guid, BuildRecord> _builds = new();

    /// <summary>Diccionario concurrente de logs indexados por BuildRecordId.</summary>
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<BuildLog>> _logs = new();

    /// <summary>
    /// Agrega un nuevo build al store.
    /// </summary>
    /// <param name="build">Build a registrar.</param>
    /// <returns>true si se agregó, false si ya existía.</returns>
    public bool AddBuild(BuildRecord build)
    {
        return _builds.TryAdd(build.Id, build);
    }

    /// <summary>
    /// Obtiene un build por su ID.
    /// </summary>
    /// <param name="buildId">ID del build.</param>
    /// <returns>El build o null si no existe.</returns>
    public BuildRecord? GetBuild(Guid buildId)
    {
        return _builds.TryGetValue(buildId, out BuildRecord? build) ? build : null;
    }

    /// <summary>
    /// Actualiza un build existente aplicando una acción de mutación.
    /// Thread-safe: la acción se ejecuta sobre la referencia almacenada.
    /// </summary>
    /// <param name="buildId">ID del build a actualizar.</param>
    /// <param name="updateAction">Acción que modifica las propiedades del build.</param>
    /// <returns>true si se encontró y actualizó, false si no existe.</returns>
    public bool UpdateBuild(Guid buildId, Action<BuildRecord> updateAction)
    {
        if (_builds.TryGetValue(buildId, out BuildRecord? build))
        {
            updateAction(build);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Obtiene todos los builds con paginación y filtros opcionales.
    /// Ordenados por fecha de creación descendente (más recientes primero).
    /// </summary>
    /// <param name="page">Número de página (1-based).</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="status">Filtro opcional por estado.</param>
    /// <param name="branch">Filtro opcional por rama.</param>
    /// <param name="repositoryUrl">Filtro opcional por URL del repositorio.</param>
    /// <returns>Tupla con la lista paginada y el total de registros.</returns>
    public (IReadOnlyList<BuildRecord> Items, int TotalCount) GetBuilds(
        int page = 1,
        int pageSize = 20,
        BuildStatus? status = null,
        string? branch = null,
        string? repositoryUrl = null)
    {
        IEnumerable<BuildRecord> query = _builds.Values;

        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(branch))
        {
            query = query.Where(b => b.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(repositoryUrl))
        {
            query = query.Where(b => b.RepositoryUrl.Contains(repositoryUrl, StringComparison.OrdinalIgnoreCase));
        }

        List<BuildRecord> ordered = query
            .OrderByDescending(b => b.CreatedAt)
            .ToList();

        int totalCount = ordered.Count;
        int skip = (page - 1) * pageSize;

        List<BuildRecord> items = ordered
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    /// <summary>
    /// Agrega un log a un build específico.
    /// </summary>
    /// <param name="buildId">ID del build.</param>
    /// <param name="log">Entrada de log a agregar.</param>
    public void AddLog(Guid buildId, BuildLog log)
    {
        ConcurrentBag<BuildLog> logs = _logs.GetOrAdd(buildId, _ => []);
        logs.Add(log);
    }

    /// <summary>
    /// Obtiene todos los logs de un build, ordenados por timestamp ascendente.
    /// </summary>
    /// <param name="buildId">ID del build.</param>
    /// <returns>Lista de logs ordenados cronológicamente.</returns>
    public IReadOnlyList<BuildLog> GetLogs(Guid buildId)
    {
        if (_logs.TryGetValue(buildId, out ConcurrentBag<BuildLog>? logs))
        {
            return logs.OrderBy(l => l.Timestamp).ToList();
        }
        return [];
    }

    /// <summary>
    /// Retorna el número total de builds almacenados.
    /// Útil para health checks y métricas.
    /// </summary>
    public int Count => _builds.Count;
}
