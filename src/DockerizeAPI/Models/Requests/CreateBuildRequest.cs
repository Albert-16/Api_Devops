using System.ComponentModel.DataAnnotations;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Request para iniciar un nuevo build de imagen Docker.
/// Contiene la URL del repositorio, credenciales, configuración de imagen y del registry.
/// </summary>
public sealed record CreateBuildRequest
{
    /// <summary>URL del repositorio en Gitea a clonar y construir.</summary>
    [Required(ErrorMessage = "repositoryUrl es requerido")]
    [Url(ErrorMessage = "repositoryUrl debe ser una URL válida")]
    public required string RepositoryUrl { get; init; }

    /// <summary>Rama a clonar. Default: main</summary>
    [StringLength(100, ErrorMessage = "branch no puede exceder 100 caracteres")]
    public string Branch { get; init; } = "main";

    /// <summary>Token de acceso Gitea. También se usa como REPO_TOKEN para paquetes Debian y login al registry.</summary>
    [Required(ErrorMessage = "gitToken es requerido")]
    public required string GitToken { get; init; }

    /// <summary>Configuración de la imagen a construir.</summary>
    public ImageConfigRequest? ImageConfig { get; init; }

    /// <summary>Configuración del Container Registry donde se publicará la imagen.</summary>
    public RegistryConfigRequest? RegistryConfig { get; init; }

    /// <summary>Si es true, ejecuta el pipeline en modo sandbox (simulado, sin operaciones reales de git/docker).</summary>
    public bool Sandbox { get; init; }

    /// <summary>Si es true en modo sandbox, simula un fallo en el paso indicado por FailAtStep.</summary>
    public bool SimulateFailure { get; init; }

    /// <summary>Paso en el que simular el fallo (Cloning, Building, Pushing). Solo aplica si Sandbox y SimulateFailure son true.</summary>
    public string? FailAtStep { get; init; }
}
