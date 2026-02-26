namespace DockerizeAPI.Configuration;

/// <summary>
/// Configuración para el proceso de deploy de containers Docker.
/// </summary>
public sealed class DeploySettings
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string SectionName = "Deploy";

    /// <summary>Número máximo de deploys ejecutándose simultáneamente. Default: 3</summary>
    public int MaxConcurrentDeploys { get; init; } = 3;

    /// <summary>Timeout máximo por deploy en minutos. Default: 5</summary>
    public int TimeoutMinutes { get; init; } = 5;
}
