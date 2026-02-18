using System.ComponentModel.DataAnnotations;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Configuración del Container Registry donde se publicará la imagen.
/// Si no se proporciona, se usan los valores por defecto de appsettings.json.
/// </summary>
public sealed record RegistryConfigRequest
{
    /// <summary>URL del Container Registry. Default: repos.daviviendahn.dvhn</summary>
    [StringLength(200)]
    public string? RegistryUrl { get; init; }

    /// <summary>Organización/owner en Gitea. Default: davivienda-banco</summary>
    [StringLength(100)]
    public string? Owner { get; init; }

    /// <summary>Nombre del repositorio para el paquete. Si es null, se usa el nombre del repo.</summary>
    [StringLength(200)]
    public string? Repository { get; init; }
}
