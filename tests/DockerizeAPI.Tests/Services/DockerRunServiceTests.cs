using DockerizeAPI.Models.Enums;
using DockerizeAPI.Services;
using DockerizeAPI.Services.Interfaces;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para DockerRunService.
/// Verifica la construcción correcta de argumentos de docker run y conversión de restart policy.
/// </summary>
public sealed class DockerRunServiceTests
{
    [Fact]
    public void BuildDockerRunArguments_Basico_GeneraComandoCorrecto()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "repos.dvhn/org/myapp:latest",
            ContainerName = "myapp",
            Detached = true,
            Network = "bridge"
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.StartsWith("run", result);
        Assert.Contains("-d", result);
        Assert.Contains("--name myapp", result);
        Assert.Contains("--network bridge", result);
        Assert.EndsWith("repos.dvhn/org/myapp:latest", result);
    }

    [Fact]
    public void BuildDockerRunArguments_ConPuertos_AgregaFlagP()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "myapp:latest",
            ContainerName = "myapp",
            Ports = ["8080:80", "443:443"]
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.Contains("-p 8080:80", result);
        Assert.Contains("-p 443:443", result);
    }

    [Fact]
    public void BuildDockerRunArguments_ConVolumenes_AgregaFlagV()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "myapp:latest",
            ContainerName = "myapp",
            Volumes = ["/host/data:/app/data", "/host/logs:/app/logs"]
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.Contains("-v /host/data:/app/data", result);
        Assert.Contains("-v /host/logs:/app/logs", result);
    }

    [Fact]
    public void BuildDockerRunArguments_ConVariablesEntorno_AgregaFlagE()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "myapp:latest",
            ContainerName = "myapp",
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["DB_HOST"] = "localhost",
                ["DB_PORT"] = "5432"
            }
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.Contains("-e \"DB_HOST=localhost\"", result);
        Assert.Contains("-e \"DB_PORT=5432\"", result);
    }

    [Fact]
    public void BuildDockerRunArguments_ConRestartPolicy_AgregaFlag()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "myapp:latest",
            ContainerName = "myapp",
            RestartPolicy = "unless-stopped"
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.Contains("--restart unless-stopped", result);
    }

    [Fact]
    public void BuildDockerRunArguments_RestartPolicyNo_NoAgregaFlag()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "myapp:latest",
            ContainerName = "myapp",
            RestartPolicy = "no"
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.DoesNotContain("--restart", result);
    }

    [Fact]
    public void BuildDockerRunArguments_Interactive_AgregaFlagI()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "myapp:latest",
            ContainerName = "myapp",
            Interactive = true,
            Detached = false
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.Contains("-i", result);
        Assert.DoesNotContain("-d", result);
    }

    [Fact]
    public void BuildDockerRunArguments_Completo_GeneraComandoCompleto()
    {
        // Arrange
        var options = new DockerRunOptions
        {
            ImageName = "repos.dvhn/org/ms23:sapp-dev",
            ContainerName = "ms23-auth",
            Detached = true,
            RestartPolicy = "unless-stopped",
            Network = "host",
            Ports = ["8080:80"],
            Volumes = ["/data:/app/data"],
            EnvironmentVariables = new Dictionary<string, string> { ["ENV"] = "production" }
        };

        // Act
        string result = DockerRunService.BuildDockerRunArguments(options);

        // Assert
        Assert.StartsWith("run -d", result);
        Assert.Contains("--name ms23-auth", result);
        Assert.Contains("-p 8080:80", result);
        Assert.Contains("-v /data:/app/data", result);
        Assert.Contains("-e \"ENV=production\"", result);
        Assert.Contains("--restart unless-stopped", result);
        Assert.Contains("--network host", result);
        Assert.EndsWith("repos.dvhn/org/ms23:sapp-dev", result);
    }

    // ─── RestartPolicyToString Tests ───

    [Theory]
    [InlineData(RestartPolicy.No, 0, "no")]
    [InlineData(RestartPolicy.Always, 0, "always")]
    [InlineData(RestartPolicy.UnlessStopped, 0, "unless-stopped")]
    [InlineData(RestartPolicy.OnFailure, 0, "on-failure")]
    [InlineData(RestartPolicy.OnFailure, 3, "on-failure:3")]
    public void RestartPolicyToString_ConvierteCorrectamente(RestartPolicy policy, int maxRetries, string expected)
    {
        // Act
        string result = DockerRunService.RestartPolicyToString(policy, maxRetries);

        // Assert
        Assert.Equal(expected, result);
    }
}
