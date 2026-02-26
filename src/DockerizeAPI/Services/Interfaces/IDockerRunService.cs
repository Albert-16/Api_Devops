namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para operaciones Docker de gestión de containers.
/// Incluye: login, pull, run, stop, remove, restart, inspect, logs.
/// </summary>
public interface IDockerRunService
{
    /// <summary>
    /// Autentica contra un container registry usando docker login.
    /// </summary>
    /// <param name="registryUrl">URL del registry.</param>
    /// <param name="username">Usuario (generalmente "token").</param>
    /// <param name="password">Token de acceso.</param>
    /// <param name="deployId">ID del deploy para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si el login fue exitoso.</returns>
    Task<bool> LoginAsync(string registryUrl, string username, string password, Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarga una imagen desde el container registry.
    /// </summary>
    /// <param name="imageName">Nombre completo de la imagen con tag.</param>
    /// <param name="deployId">ID del deploy para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si el pull fue exitoso.</returns>
    Task<bool> PullImageAsync(string imageName, Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea y arranca un container Docker con la configuración especificada.
    /// </summary>
    /// <param name="options">Opciones de ejecución del container.</param>
    /// <param name="deployId">ID del deploy para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Container ID si fue exitoso, null si falló.</returns>
    Task<string?> RunContainerAsync(DockerRunOptions options, Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si existe un container con el nombre dado (cualquier estado).
    /// </summary>
    /// <param name="containerName">Nombre del container.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si existe.</returns>
    Task<bool> ContainerExistsAsync(string containerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene un container en ejecución.
    /// </summary>
    /// <param name="containerName">Nombre del container.</param>
    /// <param name="deployId">ID del deploy para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si se detuvo exitosamente.</returns>
    Task<bool> StopContainerAsync(string containerName, Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un container detenido.
    /// </summary>
    /// <param name="containerName">Nombre del container.</param>
    /// <param name="deployId">ID del deploy para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si se eliminó exitosamente.</returns>
    Task<bool> RemoveContainerAsync(string containerName, Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reinicia un container en ejecución.
    /// </summary>
    /// <param name="containerName">Nombre del container.</param>
    /// <param name="deployId">ID del deploy para broadcasting de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si se reinició exitosamente.</returns>
    Task<bool> RestartContainerAsync(string containerName, Guid deployId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta docker inspect sobre un container y retorna el JSON.
    /// </summary>
    /// <param name="containerName">Nombre del container.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>JSON de docker inspect o null si no existe.</returns>
    Task<string?> InspectContainerAsync(string containerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si un container está actualmente en ejecución.
    /// </summary>
    /// <param name="containerName">Nombre del container.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>true si está corriendo.</returns>
    Task<bool> IsContainerRunningAsync(string containerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los logs de un container.
    /// </summary>
    /// <param name="containerName">Nombre del container.</param>
    /// <param name="tail">Número de líneas finales a obtener. null para todas.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Contenido de los logs.</returns>
    Task<string> GetContainerLogsAsync(string containerName, int? tail = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Opciones para docker run. Contiene toda la configuración del container.
/// </summary>
public sealed record DockerRunOptions
{
    /// <summary>Nombre completo de la imagen con tag.</summary>
    public required string ImageName { get; init; }

    /// <summary>Nombre del container.</summary>
    public required string ContainerName { get; init; }

    /// <summary>Si true, ejecuta en modo detached (-d).</summary>
    public bool Detached { get; init; } = true;

    /// <summary>Si true, ejecuta en modo interactivo (-i).</summary>
    public bool Interactive { get; init; }

    /// <summary>Política de reinicio (--restart). Ejemplo: "no", "always", "unless-stopped", "on-failure:3".</summary>
    public string RestartPolicy { get; init; } = "no";

    /// <summary>Red Docker (--network). null = no se agrega el flag.</summary>
    public string? Network { get; init; }

    /// <summary>Mapeo de puertos (-p). Ejemplo: ["8080:80", "443:443"].</summary>
    public IReadOnlyList<string> Ports { get; init; } = [];

    /// <summary>Montajes de volúmenes (-v). Ejemplo: ["/host:/container"].</summary>
    public IReadOnlyList<string> Volumes { get; init; } = [];

    /// <summary>Variables de entorno (-e).</summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();
}
