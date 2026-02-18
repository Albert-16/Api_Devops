using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;

namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el generador de Dockerfiles a partir de templates.
/// Reemplaza placeholders ({{csprojPath}}, {{csprojDir}}, {{assemblyName}}) con valores reales.
/// </summary>
public interface IDockerfileGenerator
{
    /// <summary>
    /// Genera un Dockerfile reemplazando los placeholders del template.
    /// </summary>
    /// <param name="useOdbc">true para template Debian con ODBC, false para Alpine.</param>
    /// <param name="csprojPath">Ruta relativa al .csproj.</param>
    /// <param name="assemblyName">Nombre del assembly (opcional, se extrae del .csproj si es null).</param>
    /// <returns>Contenido del Dockerfile generado.</returns>
    string Generate(bool useOdbc, string csprojPath, string? assemblyName = null);

    /// <summary>
    /// Genera un preview del Dockerfile sin ejecutar el build.
    /// </summary>
    /// <param name="request">Datos para la preview.</param>
    /// <returns>Preview con el Dockerfile generado, tipo de template y placeholders usados.</returns>
    DockerfilePreviewResponse Preview(PreviewDockerfileRequest request);
}
