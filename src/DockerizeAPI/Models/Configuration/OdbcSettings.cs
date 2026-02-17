namespace DockerizeAPI.Models.Configuration;

/// <summary>
/// Configuracion de los paquetes ODBC para builds con template Debian.
/// Se carga desde la seccion "OdbcPackages" del appsettings.json.
/// </summary>
public class OdbcSettings
{
    /// <summary>
    /// Nombre de la seccion en appsettings.json.
    /// </summary>
    public const string SectionName = "OdbcPackages";

    /// <summary>
    /// URL del repositorio Debian privado en Gitea que contiene los paquetes ODBC.
    /// </summary>
    public string DebianRepoUrl { get; set; } = "https://repos.daviviendahn.dvhn/api/packages/Davivienda-Banco/debian";

    /// <summary>
    /// URL de la key publica del repositorio Debian.
    /// </summary>
    public string DebianKeyUrl { get; set; } = "https://repos.daviviendahn.dvhn/api/packages/Davivienda-Banco/debian/repository.key";

    /// <summary>
    /// Distribucion Debian a usar. Default: "bookworm".
    /// </summary>
    public string Distribution { get; set; } = "bookworm";

    /// <summary>
    /// Componente del repositorio Debian. Default: "main".
    /// </summary>
    public string Component { get; set; } = "main";

    /// <summary>
    /// Lista de paquetes ODBC a instalar en la imagen Debian.
    /// </summary>
    public List<string> Packages { get; set; } =
    [
        "unixodbc",
        "unixodbc-common",
        "unixodbc-dev",
        "libodbc2",
        "libodbcinst2",
        "libodbccr2",
        "odbcinst",
        "libltdl7",
        "libreadline8",
        "readline-common",
        "ibm-iaccess"
    ];
}
