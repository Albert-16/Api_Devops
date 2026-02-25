using DockerizeAPI.Configuration;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DockerizeAPI.Services;

/// <summary>
/// Copia archivos compartidos de soporte (certificados CA, nuget.config, wget .debs)
/// desde el directorio configurado en <c>BuildSettings.SharedFilesPath</c>
/// al subdirectorio <c>.tmp/</c> del workspace de build.
/// </summary>
public sealed class SharedFilesService : ISharedFilesService
{
    private readonly BuildSettings _buildSettings;
    private readonly IBuildLogBroadcaster _broadcaster;
    private readonly ILogger<SharedFilesService> _logger;

    public SharedFilesService(
        IOptions<BuildSettings> buildSettings,
        IBuildLogBroadcaster broadcaster,
        ILogger<SharedFilesService> logger)
    {
        _buildSettings = buildSettings.Value;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CopyToWorkspaceAsync(
        string workspacePath,
        bool includeOdbcDependencies,
        Guid buildId,
        CancellationToken cancellationToken)
    {
        string sharedPath = _buildSettings.SharedFilesPath;

        if (!Directory.Exists(sharedPath))
        {
            throw new DirectoryNotFoundException(
                $"El directorio de archivos compartidos no existe: {sharedPath}");
        }

        string tmpBase = Path.Combine(workspacePath, ".tmp");

        await _broadcaster.BroadcastLogAsync(buildId,
            $"Copiando archivos compartidos desde {sharedPath}", cancellationToken: cancellationToken);

        // 1. Certificado CA → .tmp/certificate/ca/
        string certSource = Path.Combine(sharedPath, "ca-davivienda.crt");
        string certDest = Path.Combine(tmpBase, "certificate", "ca");
        Directory.CreateDirectory(certDest);

        if (File.Exists(certSource))
        {
            File.Copy(certSource, Path.Combine(certDest, "ca-davivienda.crt"), overwrite: true);
            await _broadcaster.BroadcastLogAsync(buildId,
                "Certificado CA copiado a .tmp/certificate/ca/", cancellationToken: cancellationToken);
        }
        else
        {
            _logger.LogWarning("Certificado CA no encontrado en {Path}", certSource);
            await _broadcaster.BroadcastLogAsync(buildId,
                $"Advertencia: certificado CA no encontrado en {certSource}", "warning", cancellationToken);
        }

        // 2. nuget.config → .tmp/nuget/
        string nugetSource = Path.Combine(sharedPath, "nuget.config");
        string nugetDest = Path.Combine(tmpBase, "nuget");
        Directory.CreateDirectory(nugetDest);

        if (File.Exists(nugetSource))
        {
            File.Copy(nugetSource, Path.Combine(nugetDest, "nuget.config"), overwrite: true);
            await _broadcaster.BroadcastLogAsync(buildId,
                "nuget.config copiado a .tmp/nuget/", cancellationToken: cancellationToken);
        }
        else
        {
            _logger.LogWarning("nuget.config no encontrado en {Path}", nugetSource);
            await _broadcaster.BroadcastLogAsync(buildId,
                $"Advertencia: nuget.config no encontrado en {nugetSource}", "warning", cancellationToken);
        }

        // 3. wget (.deb files) → .tmp/wget/ — solo si ODBC
        if (includeOdbcDependencies)
        {
            string wgetDest = Path.Combine(tmpBase, "wget");
            Directory.CreateDirectory(wgetDest);

            int copiedCount = 0;

            // Copiar directorio wget/ si existe
            string wgetDirSource = Path.Combine(sharedPath, "wget");
            if (Directory.Exists(wgetDirSource))
            {
                foreach (string file in Directory.GetFiles(wgetDirSource))
                {
                    string destFile = Path.Combine(wgetDest, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                    copiedCount++;
                }
            }

            // Copiar archivos .deb sueltos del root
            foreach (string debFile in Directory.GetFiles(sharedPath, "*.deb"))
            {
                string destFile = Path.Combine(wgetDest, Path.GetFileName(debFile));
                File.Copy(debFile, destFile, overwrite: true);
                copiedCount++;
            }

            await _broadcaster.BroadcastLogAsync(buildId,
                $"wget/ODBC: {copiedCount} archivo(s) copiados a .tmp/wget/", cancellationToken: cancellationToken);
        }

        await _broadcaster.BroadcastLogAsync(buildId,
            "Archivos compartidos copiados al workspace exitosamente", cancellationToken: cancellationToken);

        _logger.LogInformation("Shared files copiados al workspace {WorkspacePath} para build {BuildId}",
            workspacePath, buildId);
    }
}
