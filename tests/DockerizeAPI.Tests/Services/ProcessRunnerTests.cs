using DockerizeAPI.Services;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para ProcessRunner.
/// Verifica la sanitización de logs y ejecución de procesos.
/// </summary>
public sealed class ProcessRunnerTests
{
    [Theory]
    [InlineData(
        "clone https://mytoken123@repos.dvhn/org/repo.git /tmp",
        "clone https://[REDACTED]@repos.dvhn/org/repo.git /tmp")]
    [InlineData(
        "login -u token -p secretpass123 repos.dvhn",
        "login -u token -p [REDACTED] repos.dvhn")]
    [InlineData(
        "--build-arg REPO_TOKEN=abc123secret --tag img:latest",
        "--build-arg REPO_TOKEN=[REDACTED] --tag img:latest")]
    public void SanitizeForLogging_OcultaCredenciales(string input, string expected)
    {
        // Act
        string result = ProcessRunner.SanitizeForLogging(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeForLogging_SinCredenciales_NoModifica()
    {
        // Arrange
        string input = "bud --tag image:latest --platform linux/amd64 .";

        // Act
        string result = ProcessRunner.SanitizeForLogging(input);

        // Assert
        Assert.Equal(input, result);
    }

    [Fact]
    public void SanitizeForLogging_StringVacio_RetornaVacio()
    {
        // Act
        string result = ProcessRunner.SanitizeForLogging("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void SanitizeForLogging_MultipleTokens_SanitizaTodos()
    {
        // Arrange
        string input = "clone https://token1@host1.com/repo.git -p secret123 REPO_TOKEN=value";

        // Act
        string result = ProcessRunner.SanitizeForLogging(input);

        // Assert
        Assert.DoesNotContain("token1", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("value", result);
        Assert.Contains("[REDACTED]", result);
    }
}
