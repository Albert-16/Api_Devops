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
}
