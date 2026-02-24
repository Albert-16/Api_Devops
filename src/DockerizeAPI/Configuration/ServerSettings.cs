namespace DockerizeAPI.Configuration;

/// <summary>
/// Configuración del servidor HTTP y features de la API.
/// Permite configurar URL, puerto y habilitación de Swagger desde appsettings.json.
/// </summary>
public sealed class ServerSettings
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string SectionName = "Server";

    /// <summary>
    /// URL(s) donde escucha la API. Ejemplo: "http://+:8080", "http://localhost:5050".
    /// Múltiples URLs separadas por punto y coma: "http://+:8080;https://+:8443".
    /// </summary>
    public string Urls { get; init; } = "http://+:8080";

    /// <summary>
    /// Si true, habilita Swagger UI en cualquier entorno (no solo Development).
    /// Default: true.
    /// </summary>
    public bool EnableSwagger { get; init; } = true;
}
