namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Wrapper genérico para respuestas paginadas.
/// Incluye metadatos de paginación para facilitar la navegación del cliente.
/// </summary>
/// <typeparam name="T">Tipo de los items en la página.</typeparam>
public sealed record PagedResponse<T>
{
    /// <summary>Lista de items en la página actual.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Número de página actual (1-based).</summary>
    public required int Page { get; init; }

    /// <summary>Cantidad de items por página.</summary>
    public required int PageSize { get; init; }

    /// <summary>Cantidad total de items en todas las páginas.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Cantidad total de páginas.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Indica si existe una página siguiente.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Indica si existe una página anterior.</summary>
    public bool HasPreviousPage => Page > 1;
}
