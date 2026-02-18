namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el wrapper de Buildah CLI.
/// Gestiona construcción, login, push y cleanup de imágenes OCI.
/// </summary>
public interface IBuildahService
{
    /// <summary>
    /// Construye una imagen usando buildah bud con los flags configurados.
    /// </summary>
    /// <param name="contextPath">Directorio de contexto del build.</param>
    /// <param name="dockerfilePath">Ruta al Dockerfile generado.</param>
    /// <param name="fullImageTag">Tag completo de la imagen (registry/owner/name:tag).</param>
    /// <param name="buildId">ID del build para broadcasting de logs.</param>
    /// <param name="options">Opciones adicionales del build.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si el build fue exitoso.</returns>
    Task<bool> BuildImageAsync(
        string contextPath,
        string dockerfilePath,
        string fullImageTag,
        Guid buildId,
        BuildahBuildOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Autentica contra un container registry usando buildah login.
    /// </summary>
    /// <param name="registryUrl">URL del registry.</param>
    /// <param name="username">Usuario (generalmente "token").</param>
    /// <param name="password">Token de acceso.</param>
    /// <param name="buildId">ID del build para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si el login fue exitoso.</returns>
    Task<bool> LoginAsync(
        string registryUrl,
        string username,
        string password,
        Guid buildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publica una imagen al container registry usando buildah push.
    /// </summary>
    /// <param name="fullImageTag">Tag completo de la imagen.</param>
    /// <param name="buildId">ID del build para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si el push fue exitoso.</returns>
    Task<bool> PushImageAsync(
        string fullImageTag,
        Guid buildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina la imagen local después del push para liberar espacio.
    /// </summary>
    /// <param name="fullImageTag">Tag completo de la imagen a eliminar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task CleanupImageAsync(string fullImageTag, CancellationToken cancellationToken = default);
}

/// <summary>
/// Opciones adicionales para buildah bud.
/// </summary>
public sealed class BuildahBuildOptions
{
    /// <summary>Plataforma objetivo. Default: "linux/amd64".</summary>
    public string? Platform { get; set; }

    /// <summary>Si true, no usa cache de layers.</summary>
    public bool NoCache { get; set; }

    /// <summary>Si true, siempre descarga la imagen base más reciente.</summary>
    public bool Pull { get; set; }

    /// <summary>Si true, salida mínima.</summary>
    public bool Quiet { get; set; }

    /// <summary>Modo de red durante el build.</summary>
    public string? Network { get; set; }

    /// <summary>Build args adicionales (--build-arg KEY=VALUE).</summary>
    public Dictionary<string, string>? BuildArgs { get; set; }

    /// <summary>Labels de metadata (--label KEY=VALUE).</summary>
    public Dictionary<string, string>? Labels { get; set; }
}
