using System.ComponentModel.DataAnnotations;
using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Request para crear un nuevo deploy de container Docker.
/// Incluye la imagen a desplegar, nombre del container y configuración de ejecución.
/// </summary>
public sealed record CreateDeployRequest
{
    /// <summary>Nombre completo de la imagen incluyendo registry y tag. Ejemplo: repos.daviviendahn.dvhn/davivienda-banco/myapp:latest</summary>
    [Required]
    public required string ImageName { get; init; }

    /// <summary>Token de acceso a Gitea para login al Container Registry.</summary>
    [Required]
    public required string GitToken { get; init; }

    /// <summary>Nombre del container Docker. Debe cumplir: ^[a-zA-Z0-9][a-zA-Z0-9_.-]+$</summary>
    [Required]
    [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9_.-]+$",
        ErrorMessage = "El nombre del container solo puede contener letras, números, guiones, puntos y guiones bajos, y debe iniciar con alfanumérico.")]
    public required string ContainerName { get; init; }

    /// <summary>Mapeo de puertos host:container. Ejemplo: ["8080:80", "443:443"]</summary>
    public List<string> Ports { get; init; } = [];

    /// <summary>Si es true, ejecuta el container en modo detached (-d). Default: true.</summary>
    public bool Detached { get; init; } = true;

    /// <summary>Si es true, ejecuta el container en modo interactivo (-i).</summary>
    public bool Interactive { get; init; }

    /// <summary>Política de reinicio del container. Default: No.</summary>
    public RestartPolicy RestartPolicy { get; init; } = RestartPolicy.No;

    /// <summary>Número máximo de reintentos para política OnFailure.</summary>
    public int OnFailureMaxRetries { get; init; }

    /// <summary>Montajes de volúmenes host:container. Ejemplo: ["/host/data:/app/data"]</summary>
    public List<string> Volumes { get; init; } = [];

    /// <summary>Red Docker a la que se conecta el container. Default: bridge.</summary>
    public string Network { get; init; } = "bridge";

    /// <summary>Variables de entorno para el container.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}
