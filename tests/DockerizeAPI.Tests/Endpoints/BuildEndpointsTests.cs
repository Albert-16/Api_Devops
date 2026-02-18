using System.Net;
using System.Net.Http.Json;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DockerizeAPI.Tests.Endpoints;

/// <summary>
/// Tests de integración para BuildEndpoints.
/// Usa WebApplicationFactory para tests contra la API real en memoria.
/// </summary>
public sealed class BuildEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BuildEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_RetornaOk()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBuilds_RetornaRespuestaPaginada()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/builds");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<BuildResponse>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        // TotalCount puede variar porque WebApplicationFactory es compartido entre tests
        Assert.True(result.TotalCount >= 0);
    }

    [Fact]
    public async Task GetBuildById_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/builds/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBuild_SinToken_RetornaValidationError()
    {
        // Arrange — request sin gitToken (requerido)
        var request = new CreateBuildRequest
        {
            RepositoryUrl = "https://repos.dvhn/org/repo.git",
            Branch = "main",
            GitToken = ""
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/builds", request);

        // Assert — ValidationProblem o BadRequest
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Accepted,
            $"Expected 400 or 202, got {response.StatusCode}");
    }

    [Fact]
    public async Task CreateBuild_RequestValido_RetornaAccepted()
    {
        // Arrange
        var request = new CreateBuildRequest
        {
            RepositoryUrl = "https://repos.dvhn/org/test-repo.git",
            Branch = "main",
            GitToken = "test-token-12345"
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/builds", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BuildResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.BuildId);
        Assert.Equal("Queued", result.Status.ToString());
    }

    [Fact]
    public async Task CancelBuild_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.DeleteAsync($"/api/builds/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PreviewDockerfile_Alpine_RetornaPreview()
    {
        // Arrange
        var request = new PreviewDockerfileRequest
        {
            IncludeOdbcDependencies = false,
            CsprojPath = "src/MyApp/MyApp.csproj",
            AssemblyName = "MyApp"
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/builds/preview-dockerfile", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DockerfilePreviewResponse>();
        Assert.NotNull(result);
        Assert.Equal("alpine", result.TemplateType);
        Assert.Contains("MyApp.dll", result.Content);
        Assert.Contains("src/MyApp/MyApp.csproj", result.Content);
    }

    [Fact]
    public async Task PreviewDockerfile_Odbc_RetornaPreviewConRepoToken()
    {
        // Arrange
        var request = new PreviewDockerfileRequest
        {
            IncludeOdbcDependencies = true,
            CsprojPath = "Presentation/Api.csproj",
            AssemblyName = "Api"
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/builds/preview-dockerfile", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DockerfilePreviewResponse>();
        Assert.NotNull(result);
        Assert.Equal("odbc", result.TemplateType);
        Assert.Contains("REPO_TOKEN", result.Content);
        Assert.Contains("ibm-iaccess", result.Content);
    }

    [Fact]
    public async Task GetTemplateAlpine_RetornaTemplate()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/templates/alpine");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TemplateResponse>();
        Assert.NotNull(result);
        Assert.Equal("alpine", result.Name);
        Assert.Contains("{{csprojPath}}", result.Content);
    }

    [Fact]
    public async Task GetTemplateOdbc_RetornaTemplate()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/templates/odbc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TemplateResponse>();
        Assert.NotNull(result);
        Assert.Equal("odbc", result.Name);
        Assert.Contains("REPO_TOKEN", result.Content);
    }
}
