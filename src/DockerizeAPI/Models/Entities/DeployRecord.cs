using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Entities;

/// <summary>
/// Entidad principal que representa un deploy de container Docker.
/// Almacena el estado completo del ciclo de vida del deploy, incluyendo
/// configuración de container, historial de rollback y request original para retry.
/// </summary>
public sealed class DeployRecord
{
    /// <summary>Identificador único del deploy.</summary>
    public Guid Id { get; set; }

    /// <summary>Nombre completo de la imagen (registry/owner/name:tag).</summary>
    public required string ImageName { get; set; }

    /// <summary>Nombre del container Docker.</summary>
    public required string ContainerName { get; set; }

    /// <summary>Token de acceso a Gitea (usado para login al registry).</summary>
    public required string GitToken { get; set; }

    /// <summary>Estado actual del deploy en su ciclo de vida.</summary>
    public DeployStatus Status { get; set; } = DeployStatus.Queued;

    /// <summary>Mensaje de error si el deploy falló. null si no ha fallado.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>ID del container Docker asignado por el daemon.</summary>
    public string? ContainerId { get; set; }

    // ─── Container Config ───

    /// <summary>Si es true, ejecuta el container en modo detached (-d).</summary>
    public bool Detached { get; set; } = true;

    /// <summary>Si es true, ejecuta el container en modo interactivo (-i).</summary>
    public bool Interactive { get; set; }

    /// <summary>Política de reinicio del container.</summary>
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.Always;

    /// <summary>Número máximo de reintentos para política OnFailure.</summary>
    public int OnFailureMaxRetries { get; set; }

    /// <summary>Red Docker (--network). null = no se agrega el flag, Docker usa su default.</summary>
    public string? Network { get; set; }

    /// <summary>Mapeo de puertos host:container. Ejemplo: ["8080:80", "443:443"]</summary>
    public List<string> Ports { get; set; } = [];

    /// <summary>Montajes de volúmenes host:container. Ejemplo: ["/host/data:/app/data"]</summary>
    public List<string> Volumes { get; set; } = [];

    /// <summary>Variables de entorno para el container.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    // ─── Rollback ───

    /// <summary>Versión del deploy para este container name. Se incrementa en cada re-deploy.</summary>
    public int DeployVersion { get; set; } = 1;

    /// <summary>Imagen anterior para rollback.</summary>
    public string? PreviousImageName { get; set; }

    /// <summary>Configuración anterior serializada como JSON para rollback.</summary>
    public string? PreviousConfigJson { get; set; }

    /// <summary>true si este deploy fue resultado de un rollback automático o manual.</summary>
    public bool IsRollback { get; set; }

    // ─── Timestamps ───

    /// <summary>Fecha y hora UTC de creación del deploy.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Fecha y hora UTC de inicio del procesamiento.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Fecha y hora UTC de finalización (éxito o fallo).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    // ─── Registry ───

    /// <summary>URL del registry desde donde se descarga la imagen.</summary>
    public string RegistryUrl { get; set; } = "repos.daviviendahn.dvhn";

    // ─── Retry ───

    /// <summary>Número de veces que se ha reintentado este deploy.</summary>
    public int RetryCount { get; set; }

    /// <summary>Configuración original del request serializada como JSON para soportar retry.</summary>
    public string? OriginalRequestJson { get; set; }
}
