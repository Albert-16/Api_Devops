using System.Collections.Concurrent;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;

namespace DockerizeAPI.Models;

/// <summary>
/// Entidad principal que representa un proceso de build completo.
/// Almacena toda la informacion del build incluyendo configuracion,
/// estado, timestamps, logs y resultado final.
/// Es thread-safe para acceso concurrente desde el Background Service y los endpoints.
/// </summary>
public class BuildInfo
{
    /// <summary>
    /// Identificador unico del build (GUID).
    /// </summary>
    public string BuildId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Estado actual del build. Se actualiza conforme avanza el pipeline.
    /// </summary>
    public BuildStatus Status { get; set; } = BuildStatus.Queued;

    /// <summary>
    /// URL del repositorio Gitea que se esta construyendo.
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Rama del repositorio que se clono.
    /// </summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>
    /// Token de acceso a Gitea. Tambien se usa como REPO_TOKEN para paquetes Debian.
    /// </summary>
    public string GitToken { get; set; } = string.Empty;

    /// <summary>
    /// Configuracion de la imagen Docker.
    /// </summary>
    public ImageConfig ImageConfig { get; set; } = new();

    /// <summary>
    /// Configuracion del Container Registry.
    /// </summary>
    public RegistryConfig RegistryConfig { get; set; } = new();

    /// <summary>
    /// URL completa de la imagen en el Container Registry.
    /// Se establece al completar exitosamente el push.
    /// Ejemplo: "repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web:sapp-dev"
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Ruta al directorio temporal donde se clono el repositorio.
    /// Se limpia automaticamente al finalizar el build.
    /// </summary>
    public string? WorkDirectory { get; set; }

    /// <summary>
    /// Ruta relativa al archivo .csproj detectado en el repositorio.
    /// </summary>
    public string? CsprojPath { get; set; }

    /// <summary>
    /// Nombre del assembly extraido del .csproj.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Fecha y hora de creacion del build.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha y hora de inicio de la clonacion.
    /// </summary>
    public DateTime? CloningStartedAt { get; set; }

    /// <summary>
    /// Fecha y hora de inicio de la construccion.
    /// </summary>
    public DateTime? BuildingStartedAt { get; set; }

    /// <summary>
    /// Fecha y hora de inicio del push al registry.
    /// </summary>
    public DateTime? PushingStartedAt { get; set; }

    /// <summary>
    /// Fecha y hora de finalizacion del build.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Mensaje de error si el build fallo.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Logs completos del build. Thread-safe para acceso concurrente.
    /// Se agregan lineas conforme avanza cada paso del pipeline.
    /// </summary>
    public ConcurrentBag<string> Logs { get; set; } = [];

    /// <summary>
    /// Token de cancelacion para abortar el build en progreso.
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
}
