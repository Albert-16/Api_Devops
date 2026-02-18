using DockerizeAPI.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para TemplateService.
/// Verifica lectura de templates embebidos y manejo de errores.
/// </summary>
public sealed class TemplateServiceTests
{
    private readonly TemplateService _service;

    public TemplateServiceTests()
    {
        ILogger<TemplateService> logger = Substitute.For<ILogger<TemplateService>>();
        _service = new TemplateService(logger);
    }

    [Fact]
    public void GetTemplate_Alpine_RetornaTemplateEmbebido()
    {
        // Act
        var result = _service.GetTemplate("alpine");

        // Assert
        Assert.Equal("alpine", result.Name);
        Assert.False(result.IsOverride);
        Assert.Contains("dotnet-sdk:10.0-alpine", result.Content);
        Assert.Contains("{{csprojPath}}", result.Content);
        Assert.Contains("{{assemblyName}}", result.Content);
    }

    [Fact]
    public void GetTemplate_Odbc_RetornaTemplateEmbebido()
    {
        // Act
        var result = _service.GetTemplate("odbc");

        // Assert
        Assert.Equal("odbc", result.Name);
        Assert.False(result.IsOverride);
        Assert.Contains("dotnet-sdk:10.0 AS build", result.Content);
        Assert.Contains("REPO_TOKEN", result.Content);
        Assert.Contains("ibm-iaccess", result.Content);
    }

    [Fact]
    public void GetTemplate_NombreInvalido_LanzaArgumentException()
    {
        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => _service.GetTemplate("invalid"));
        Assert.Contains("no es válido", ex.Message);
    }

    [Fact]
    public void GetTemplateContent_SinOdbc_RetornaAlpine()
    {
        // Act
        string content = _service.GetTemplateContent(false);

        // Assert
        Assert.Contains("alpine", content.ToLowerInvariant());
    }

    [Fact]
    public void GetTemplateContent_ConOdbc_RetornaDebian()
    {
        // Act
        string content = _service.GetTemplateContent(true);

        // Assert
        Assert.Contains("REPO_TOKEN", content);
        Assert.Contains("ibm-iaccess", content);
    }

    [Theory]
    [InlineData("alpine")]
    [InlineData("ALPINE")]
    [InlineData("Alpine")]
    public void GetTemplate_CaseInsensitive(string name)
    {
        // Act
        var result = _service.GetTemplate(name);

        // Assert
        Assert.Equal("alpine", result.Name);
        Assert.NotEmpty(result.Content);
    }
}
