namespace DockerizeAPI.Models.Responses;

/// <summary>
/// Respuesta con el preview de un Dockerfile generado.
/// Muestra el Dockerfile final y los valores de los placeholders usados.
/// </summary>
public sealed record DockerfilePreviewResponse
{
    /// <summary>Contenido completo del Dockerfile generado con los placeholders reemplazados.</summary>
    public required string Content { get; init; }

    /// <summary>Tipo de template usado: "alpine" u "odbc".</summary>
    public required string TemplateType { get; init; }

    /// <summary>Diccionario con los placeholders y sus valores reemplazados.</summary>
    public required Dictionary<string, string> Placeholders { get; init; }
}
