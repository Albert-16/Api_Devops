using DockerizeAPI.Configuration;
using DockerizeAPI.Services;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para SharedFilesService.
/// Verifica la copia de archivos compartidos al workspace de build.
/// </summary>
public sealed class SharedFilesServiceTests : IDisposable
{
    private readonly string _sharedFilesDir;
    private readonly string _workspaceDir;
    private readonly IBuildLogBroadcaster _broadcaster;
    private readonly ILogger<SharedFilesService> _logger;

    public SharedFilesServiceTests()
    {
        string basePath = Path.Combine(Path.GetTempPath(), "SharedFilesTests_" + Guid.NewGuid().ToString("N"));
        _sharedFilesDir = Path.Combine(basePath, "shared");
        _workspaceDir = Path.Combine(basePath, "workspace");

        Directory.CreateDirectory(_sharedFilesDir);
        Directory.CreateDirectory(_workspaceDir);

        _broadcaster = Substitute.For<IBuildLogBroadcaster>();
        _logger = Substitute.For<ILogger<SharedFilesService>>();
    }

    public void Dispose()
    {
        try
        {
            string basePath = Directory.GetParent(_sharedFilesDir)!.FullName;
            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, recursive: true);
            }
        }
        catch { /* cleanup best-effort */ }
    }

    private SharedFilesService CreateService(string? sharedPath = null)
    {
        BuildSettings settings = new() { SharedFilesPath = sharedPath ?? _sharedFilesDir };
        IOptions<BuildSettings> options = Options.Create(settings);
        return new SharedFilesService(options, _broadcaster, _logger);
    }

    [Fact]
    public async Task CopyToWorkspaceAsync_CopiaCertificadoCA()
    {
        // Arrange
        string certPath = Path.Combine(_sharedFilesDir, "ca-davivienda.crt");
        await File.WriteAllTextAsync(certPath, "CERT-CONTENT");

        SharedFilesService sut = CreateService();
        Guid buildId = Guid.NewGuid();

        // Act
        await sut.CopyToWorkspaceAsync(_workspaceDir, includeOdbcDependencies: false, buildId, CancellationToken.None);

        // Assert
        string destCert = Path.Combine(_workspaceDir, ".tmp", "certificate", "ca", "ca-davivienda.crt");
        Assert.True(File.Exists(destCert));
        Assert.Equal("CERT-CONTENT", await File.ReadAllTextAsync(destCert));
    }

    [Fact]
    public async Task CopyToWorkspaceAsync_CopiaNugetConfig()
    {
        // Arrange
        string nugetPath = Path.Combine(_sharedFilesDir, "nuget.config");
        await File.WriteAllTextAsync(nugetPath, "<configuration />");

        SharedFilesService sut = CreateService();
        Guid buildId = Guid.NewGuid();

        // Act
        await sut.CopyToWorkspaceAsync(_workspaceDir, includeOdbcDependencies: false, buildId, CancellationToken.None);

        // Assert
        string destNuget = Path.Combine(_workspaceDir, ".tmp", "nuget", "nuget.config");
        Assert.True(File.Exists(destNuget));
        Assert.Equal("<configuration />", await File.ReadAllTextAsync(destNuget));
    }

    [Fact]
    public async Task CopyToWorkspaceAsync_CopiaWgetCuandoOdbcTrue()
    {
        // Arrange
        string wgetDir = Path.Combine(_sharedFilesDir, "wget");
        Directory.CreateDirectory(wgetDir);
        await File.WriteAllTextAsync(Path.Combine(wgetDir, "wget.deb"), "DEB1");
        await File.WriteAllTextAsync(Path.Combine(wgetDir, "libssl.deb"), "DEB2");

        // .deb suelto en root
        await File.WriteAllTextAsync(Path.Combine(_sharedFilesDir, "extra.deb"), "DEB3");

        SharedFilesService sut = CreateService();
        Guid buildId = Guid.NewGuid();

        // Act
        await sut.CopyToWorkspaceAsync(_workspaceDir, includeOdbcDependencies: true, buildId, CancellationToken.None);

        // Assert
        string destWget = Path.Combine(_workspaceDir, ".tmp", "wget");
        Assert.True(Directory.Exists(destWget));
        Assert.True(File.Exists(Path.Combine(destWget, "wget.deb")));
        Assert.True(File.Exists(Path.Combine(destWget, "libssl.deb")));
        Assert.True(File.Exists(Path.Combine(destWget, "extra.deb")));
    }

    [Fact]
    public async Task CopyToWorkspaceAsync_NoCopiaWgetCuandoOdbcFalse()
    {
        // Arrange
        string wgetDir = Path.Combine(_sharedFilesDir, "wget");
        Directory.CreateDirectory(wgetDir);
        await File.WriteAllTextAsync(Path.Combine(wgetDir, "wget.deb"), "DEB1");

        SharedFilesService sut = CreateService();
        Guid buildId = Guid.NewGuid();

        // Act
        await sut.CopyToWorkspaceAsync(_workspaceDir, includeOdbcDependencies: false, buildId, CancellationToken.None);

        // Assert
        string destWget = Path.Combine(_workspaceDir, ".tmp", "wget");
        Assert.False(Directory.Exists(destWget));
    }

    [Fact]
    public async Task CopyToWorkspaceAsync_ThrowSiSharedPathNoExiste()
    {
        // Arrange
        SharedFilesService sut = CreateService("/ruta/inexistente");
        Guid buildId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            sut.CopyToWorkspaceAsync(_workspaceDir, includeOdbcDependencies: false, buildId, CancellationToken.None));
    }
}
