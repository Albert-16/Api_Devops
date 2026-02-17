namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Request para generar una vista previa del Dockerfile sin ejecutar el build.
/// Util para verificar que el Dockerfile generado es correcto antes de construir.
/// </summary>
public class PreviewDockerfileRequest
{
    /// <summary>
    /// Si es true, genera el Dockerfile con template ODBC/Debian.
    /// Si es false, genera con template Alpine.
    /// Default: false.
    /// </summary>
    public bool IncludeOdbcDependencies { get; set; } = false;

    /// <summary>
    /// Ruta relativa al archivo .csproj del proyecto.
    /// Ejemplo: "1.1-Presentation/Microservices.csproj"
    /// </summary>
    public string CsprojPath { get; set; } = "Microservices.csproj";

    /// <summary>
    /// Nombre del assembly de salida (sin extension .dll).
    /// Ejemplo: "Microservices"
    /// </summary>
    public string AssemblyName { get; set; } = "Microservices";
}
