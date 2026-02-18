using System.ComponentModel.DataAnnotations;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Entrada de secreto disponible durante la construcción de la imagen.
/// Los secretos se montan como archivos temporales y no quedan en la imagen final.
/// </summary>
public sealed record SecretEntry
{
    /// <summary>Identificador del secreto (usado para referenciarlo en el Dockerfile).</summary>
    [Required]
    public required string Id { get; init; }

    /// <summary>Valor del secreto.</summary>
    [Required]
    public required string Value { get; init; }
}
