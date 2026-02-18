namespace DockerizeAPI.Models.Entities;

/// <summary>
/// Representa una línea de log generada durante el proceso de build.
/// Los logs se emiten en tiempo real vía SSE y se almacenan para consulta posterior.
/// </summary>
public sealed class BuildLog
{
    /// <summary>Identificador único de la entrada de log.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador del build al que pertenece este log.</summary>
    public Guid BuildRecordId { get; set; }

    /// <summary>Contenido del mensaje de log.</summary>
    public required string Message { get; set; }

    /// <summary>Nivel del log: info, warning, error.</summary>
    public string Level { get; set; } = "info";

    /// <summary>Fecha y hora UTC en que se generó el log.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
