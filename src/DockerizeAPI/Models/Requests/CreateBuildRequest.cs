using System.ComponentModel.DataAnnotations;

namespace DockerizeAPI.Models.Requests;

/// <summary>
/// Request para iniciar un nuevo proceso de build.
/// Contiene toda la configuracion necesaria para clonar un repositorio,
/// construir una imagen Docker y publicarla en el Gitea Container Registry.
/// </summary>
public class CreateBuildRequest
{
    /// <summary>
    /// URL del repositorio en Gitea que se va a dockerizar.
    /// Ejemplo: "https://repos.daviviendahn.dvhn/davivienda-banco/ms23-autenticacion-web"
    /// </summary>
    [Required(ErrorMessage = "La URL del repositorio es requerida.")]
    [Url(ErrorMessage = "La URL del repositorio no es valida.")]
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Rama del repositorio a clonar.
    /// Si no se especifica, se usa "main" por defecto.
    /// Ejemplo: "sapp-dev", "bel-uat"
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Token de acceso a Gitea para clonar el repositorio.
    /// Tambien se usa como REPO_TOKEN para autenticarse al repositorio Debian
    /// privado de Gitea cuando se necesitan paquetes ODBC.
    /// </summary>
    [Required(ErrorMessage = "El token de acceso a Gitea es requerido.")]
    public string GitToken { get; set; } = string.Empty;

    /// <summary>
    /// Configuracion de la imagen Docker a construir.
    /// Si no se especifica, se usan los valores por defecto.
    /// </summary>
    public ImageConfig? ImageConfig { get; set; }

    /// <summary>
    /// Configuracion del Container Registry donde se publicara la imagen.
    /// Si no se especifica, se usan los valores del appsettings.json.
    /// </summary>
    public RegistryConfig? RegistryConfig { get; set; }
}

/// <summary>
/// Configuracion especifica de la imagen Docker a construir.
/// Controla el nombre, tag, plataforma, dependencias y opciones de build.
/// </summary>
public class ImageConfig
{
    /// <summary>
    /// Nombre de la imagen. Si no se especifica, se extrae del nombre del repositorio.
    /// Ejemplo: "ms23-autenticacion-web"
    /// </summary>
    public string? ImageName { get; set; }

    /// <summary>
    /// Tag de la imagen. Si no se especifica, se usa el nombre de la rama.
    /// Ejemplo: "sapp-dev", "latest"
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Plataforma/arquitectura de la imagen. Default: "linux/amd64".
    /// </summary>
    public string Platform { get; set; } = "linux/amd64";

    /// <summary>
    /// Si es true, usa el template Debian con drivers ODBC (ibm-iaccess, unixodbc, etc.).
    /// Si es false, usa el template Alpine ligero sin ODBC.
    /// Default: false (Alpine).
    /// </summary>
    public bool IncludeOdbcDependencies { get; set; } = false;

    /// <summary>
    /// Si es true, usa multi-stage build (recomendado siempre).
    /// Stage 1: SDK para compilar. Stage 2: Runtime ligero para ejecutar.
    /// Default: true.
    /// </summary>
    public bool MultiStage { get; set; } = true;

    /// <summary>
    /// Argumentos adicionales para el build de Buildah (--build-arg KEY=VALUE).
    /// REPO_TOKEN se agrega automaticamente cuando includeOdbcDependencies es true.
    /// </summary>
    public Dictionary<string, string>? BuildArgs { get; set; }

    /// <summary>
    /// Etiquetas/metadata para la imagen Docker (--label KEY=VALUE).
    /// Ejemplo: {"maintainer": "equipo-devops", "version": "1.0.0"}
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// Tipo de red durante el build. Default: Bridge.
    /// </summary>
    public NetworkMode Network { get; set; } = NetworkMode.Bridge;

    /// <summary>
    /// Si es true, reconstruye toda la imagen sin usar cache de layers.
    /// Util cuando hay problemas con dependencias cacheadas.
    /// Default: false.
    /// </summary>
    public bool NoCache { get; set; } = false;

    /// <summary>
    /// Si es true, siempre descarga la imagen base mas reciente del registry.
    /// Default: false.
    /// </summary>
    public bool Pull { get; set; } = false;

    /// <summary>
    /// Nivel de detalle en la salida del build. Default: Auto.
    /// </summary>
    public ProgressMode Progress { get; set; } = ProgressMode.Auto;

    /// <summary>
    /// Secretos disponibles durante el build (--secret).
    /// Los secretos no quedan almacenados en la imagen final.
    /// </summary>
    public List<SecretEntry>? Secrets { get; set; }

    /// <summary>
    /// Si es true, muestra salida minima durante el build.
    /// Default: false.
    /// </summary>
    public bool Quiet { get; set; } = false;
}

/// <summary>
/// Configuracion del Container Registry de destino.
/// Define donde se publicara la imagen construida.
/// </summary>
public class RegistryConfig
{
    /// <summary>
    /// URL del Container Registry. Default: "repos.daviviendahn.dvhn".
    /// </summary>
    public string RegistryUrl { get; set; } = "repos.daviviendahn.dvhn";

    /// <summary>
    /// Organizacion/owner en el registry. Default: "davivienda-banco".
    /// </summary>
    public string Owner { get; set; } = "davivienda-banco";

    /// <summary>
    /// Nombre del repositorio para el paquete. Si no se especifica, se usa el nombre del repo.
    /// </summary>
    public string? Repository { get; set; }
}

/// <summary>
/// Entrada de secreto para usar durante el build.
/// Los secretos se pasan a Buildah via --secret y no quedan en la imagen final.
/// </summary>
public class SecretEntry
{
    /// <summary>
    /// Identificador del secreto dentro del Dockerfile.
    /// Se referencia como --mount=type=secret,id={Id}
    /// </summary>
    [Required(ErrorMessage = "El ID del secreto es requerido.")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Valor del secreto.
    /// </summary>
    [Required(ErrorMessage = "El valor del secreto es requerido.")]
    public string Value { get; set; } = string.Empty;
}
