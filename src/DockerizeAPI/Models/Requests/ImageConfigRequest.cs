using System.ComponentModel.DataAnnotations;
using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Configuración de la imagen Docker a construir.
/// Controla el template (Alpine/Debian), tags, plataforma y opciones de Buildah.
/// </summary>
public sealed record ImageConfigRequest
{
    /// <summary>Nombre de la imagen (sin tag). Si es null, se usa el nombre del repositorio.</summary>
    [StringLength(200, ErrorMessage = "imageName no puede exceder 200 caracteres")]
    public string? ImageName { get; init; }

    /// <summary>Tag de la imagen. Si es null, se usa el nombre de la rama. Ejemplo: sapp-dev</summary>
    [StringLength(128, ErrorMessage = "tag no puede exceder 128 caracteres")]
    public string? Tag { get; init; }

    /// <summary>Plataforma objetivo. Default: linux/amd64</summary>
    [StringLength(50)]
    public string Platform { get; init; } = "linux/amd64";

    /// <summary>
    /// Si es true, usa template Debian con drivers ODBC (ibm-iaccess para AS400/SQL Server).
    /// Si es false, usa template Alpine ligero (~86 MiB). Default: false
    /// </summary>
    public bool IncludeOdbcDependencies { get; init; }

    /// <summary>Usar compilación multi-stage (recomendado siempre). Default: true</summary>
    public bool MultiStage { get; init; } = true;

    /// <summary>Argumentos adicionales para el build (--build-arg KEY=VALUE).</summary>
    public Dictionary<string, string>? BuildArgs { get; init; }

    /// <summary>Etiquetas metadata para la imagen (--label KEY=VALUE).</summary>
    public Dictionary<string, string>? Labels { get; init; }

    /// <summary>Tipo de red durante el build. Default: Host (compatible con rootless).</summary>
    public NetworkMode Network { get; init; } = NetworkMode.Host;

    /// <summary>Si es true, reconstruye todo desde cero sin cache. Default: false</summary>
    public bool NoCache { get; init; }

    /// <summary>Si es true, siempre descarga la imagen base más reciente. Default: false</summary>
    public bool Pull { get; init; }

    /// <summary>Nivel de detalle en la salida del build. Default: Auto</summary>
    public ProgressMode Progress { get; init; } = ProgressMode.Auto;

    /// <summary>Secretos disponibles durante el build (--secret).</summary>
    public List<SecretEntry>? Secrets { get; init; }

    /// <summary>Si es true, muestra salida mínima. Default: false</summary>
    public bool Quiet { get; init; }
}
