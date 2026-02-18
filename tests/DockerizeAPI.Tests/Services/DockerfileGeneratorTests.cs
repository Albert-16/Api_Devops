using DockerizeAPI.Models.Requests;
using DockerizeAPI.Services;
using DockerizeAPI.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para DockerfileGenerator.
/// Verifica el reemplazo correcto de placeholders en los templates.
/// </summary>
public sealed class DockerfileGeneratorTests
{
    private readonly ITemplateService _templateService;
    private readonly DockerfileGenerator _generator;

    private const string AlpineTemplate = """
        FROM sdk AS build
        COPY {{csprojPath}} ./{{csprojDir}}/
        RUN dotnet restore {{csprojPath}}
        RUN dotnet publish {{csprojPath}} -o /app
        FROM aspnet AS final
        ENTRYPOINT ["dotnet", "{{assemblyName}}.dll"]
        """;

    public DockerfileGeneratorTests()
    {
        _templateService = Substitute.For<ITemplateService>();
        ILogger<DockerfileGenerator> logger = Substitute.For<ILogger<DockerfileGenerator>>();
        _generator = new DockerfileGenerator(_templateService, logger);
    }

    [Fact]
    public void Generate_ReemplazaTodosLosPlaceholders()
    {
        // Arrange
        _templateService.GetTemplateContent(false).Returns(AlpineTemplate);

        // Act
        string result = _generator.Generate(false, "1.1-Presentation/Microservices.csproj", "Microservices");

        // Assert
        Assert.Contains("1.1-Presentation/Microservices.csproj", result);
        Assert.Contains("1.1-Presentation/", result);
        Assert.Contains("Microservices.dll", result);
        Assert.DoesNotContain("{{csprojPath}}", result);
        Assert.DoesNotContain("{{csprojDir}}", result);
        Assert.DoesNotContain("{{assemblyName}}", result);
    }

    [Fact]
    public void Generate_SinAssemblyName_UsaNombreDelCsproj()
    {
        // Arrange
        _templateService.GetTemplateContent(false).Returns(AlpineTemplate);

        // Act
        string result = _generator.Generate(false, "src/MyApp.csproj");

        // Assert
        Assert.Contains("MyApp.dll", result);
    }

    [Fact]
    public void Generate_NormalizaSeparadoresAUnix()
    {
        // Arrange
        _templateService.GetTemplateContent(false).Returns(AlpineTemplate);

        // Act
        string result = _generator.Generate(false, "src\\MyApp\\MyApp.csproj");

        // Assert
        Assert.Contains("src/MyApp/MyApp.csproj", result);
        Assert.DoesNotContain("\\", result);
    }

    [Fact]
    public void Generate_CsprojEnRaiz_DirVacio()
    {
        // Arrange
        _templateService.GetTemplateContent(false).Returns(AlpineTemplate);

        // Act
        string result = _generator.Generate(false, "MyApp.csproj");

        // Assert
        Assert.Contains("COPY MyApp.csproj ./", result);
    }

    [Fact]
    public void Generate_SeleccionaTemplateOdbc_CuandoUseOdbcEsTrue()
    {
        // Arrange
        _templateService.GetTemplateContent(true).Returns("ODBC template {{assemblyName}}");

        // Act
        string result = _generator.Generate(true, "App.csproj", "App");

        // Assert
        _templateService.Received(1).GetTemplateContent(true);
        Assert.Contains("ODBC template App", result);
    }

    [Fact]
    public void Preview_RetornaContenidoYPlaceholders()
    {
        // Arrange
        _templateService.GetTemplateContent(false).Returns(AlpineTemplate);
        var request = new PreviewDockerfileRequest
        {
            IncludeOdbcDependencies = false,
            CsprojPath = "src/Api/Api.csproj",
            AssemblyName = "Api"
        };

        // Act
        var result = _generator.Preview(request);

        // Assert
        Assert.Equal("alpine", result.TemplateType);
        Assert.Contains("Api.dll", result.Content);
        Assert.NotNull(result.Placeholders);
        Assert.Equal("src/Api/Api.csproj", result.Placeholders["{{csprojPath}}"]);
        Assert.Equal("src/Api/", result.Placeholders["{{csprojDir}}"]);
        Assert.Equal("Api", result.Placeholders["{{assemblyName}}"]);
    }
}
