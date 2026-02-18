namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el servicio de operaciones Git.
/// Clona repositorios de Gitea, detecta .csproj y extrae metadata del proyecto.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Clona un repositorio de Gitea en el directorio especificado.
    /// </summary>
    /// <param name="repositoryUrl">URL del repositorio (con token embebido en la URL).</param>
    /// <param name="branch">Rama a clonar.</param>
    /// <param name="workspacePath">Directorio destino para el clone.</param>
    /// <param name="buildId">ID del build para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Ruta del directorio clonado.</returns>
    Task<string> CloneAsync(
        string repositoryUrl,
        string branch,
        string workspacePath,
        Guid buildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el SHA del commit actual (HEAD) del repositorio clonado.
    /// </summary>
    /// <param name="repoPath">Ruta del repositorio clonado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>SHA del commit o null si falla.</returns>
    Task<string?> GetCurrentCommitShaAsync(string repoPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detecta el archivo .csproj principal del proyecto clonado.
    /// Lógica: (1) único .csproj → úsalo, (2) OutputType=Exe, (3) carpeta Presentation/API, (4) primero.
    /// </summary>
    /// <param name="repoPath">Ruta raíz del repositorio clonado.</param>
    /// <returns>Ruta relativa al .csproj detectado (formato Unix).</returns>
    Task<string> DetectCsprojAsync(string repoPath);

    /// <summary>
    /// Extrae el AssemblyName del archivo .csproj.
    /// </summary>
    /// <param name="csprojFullPath">Ruta completa al .csproj.</param>
    /// <returns>AssemblyName o null si no se encuentra en el .csproj.</returns>
    string? ExtractAssemblyName(string csprojFullPath);
}
