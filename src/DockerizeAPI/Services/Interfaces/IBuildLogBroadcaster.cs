namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el broadcaster de logs de build en tiempo real.
/// Permite emitir logs vía SSE a clientes suscritos.
/// </summary>
public interface IBuildLogBroadcaster
{
    /// <summary>
    /// Emite un mensaje de log para un build específico.
    /// Los suscriptores lo recibirán en tiempo real vía SSE.
    /// </summary>
    /// <param name="buildId">ID del build.</param>
    /// <param name="message">Mensaje de log.</param>
    /// <param name="level">Nivel del log ("info", "warning", "error"). Default: "info".</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task BroadcastLogAsync(Guid buildId, string message, string level = "info", CancellationToken cancellationToken = default);

    /// <summary>
    /// Suscribe al stream de logs de un build. Retorna un IAsyncEnumerable para consumo con SSE.
    /// </summary>
    /// <param name="buildId">ID del build.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Stream asíncrono de líneas de log en formato JSON.</returns>
    IAsyncEnumerable<string> SubscribeAsync(Guid buildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un build como completado, cerrando su channel de logs y liberando recursos.
    /// </summary>
    /// <param name="buildId">ID del build completado.</param>
    Task CompleteBuildAsync(Guid buildId);
}
