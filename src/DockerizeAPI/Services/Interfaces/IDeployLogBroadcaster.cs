namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el broadcaster de logs de deploy en tiempo real.
/// Permite emitir logs vía SSE a clientes suscritos.
/// </summary>
public interface IDeployLogBroadcaster
{
    /// <summary>
    /// Emite un mensaje de log para un deploy específico.
    /// Los suscriptores lo recibirán en tiempo real vía SSE.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <param name="message">Mensaje de log.</param>
    /// <param name="level">Nivel del log ("info", "warning", "error"). Default: "info".</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task BroadcastLogAsync(Guid deployId, string message, string level = "info", CancellationToken cancellationToken = default);

    /// <summary>
    /// Suscribe al stream de logs de un deploy. Retorna un IAsyncEnumerable para consumo con SSE.
    /// </summary>
    /// <param name="deployId">ID del deploy.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Stream asíncrono de líneas de log en formato JSON.</returns>
    IAsyncEnumerable<string> SubscribeAsync(Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un deploy como completado, cerrando su channel de logs y liberando recursos.
    /// </summary>
    /// <param name="deployId">ID del deploy completado.</param>
    Task CompleteDeployAsync(Guid deployId);
}
