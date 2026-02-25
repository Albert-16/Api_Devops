namespace DockerizeAPI.Services.Interfaces;

/// <summary>
/// Contrato para el servicio que copia archivos compartidos de soporte
/// (certificados CA, nuget.config, wget .debs) al workspace de build.
/// </summary>
public interface ISharedFilesService
{
    /// <summary>
    /// Copia los archivos compartidos desde <c>BuildSettings.SharedFilesPath</c>
    /// al directorio <c>.tmp/</c> dentro del workspace.
    /// </summary>
    /// <param name="workspacePath">Ruta al workspace clonado del repositorio.</param>
    /// <param name="includeOdbcDependencies">Si true, incluye archivos wget (.deb) para ODBC.</param>
    /// <param name="buildId">ID del build para broadcast de logs.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task CopyToWorkspaceAsync(string workspacePath, bool includeOdbcDependencies,
        Guid buildId, CancellationToken cancellationToken);
}
