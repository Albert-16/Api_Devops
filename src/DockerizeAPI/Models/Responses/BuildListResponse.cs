namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta paginada para el listado de builds.
/// Incluye los builds que coinciden con los filtros aplicados y metadatos de paginacion.
/// </summary>
public class BuildListResponse
{
    /// <summary>
    /// Lista de builds en la pagina actual.
    /// </summary>
    public List<BuildResponse> Items { get; set; } = [];

    /// <summary>
    /// Numero de pagina actual (base 1).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Cantidad de items por pagina.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total de builds que coinciden con los filtros (todas las paginas).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total de paginas disponibles.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
