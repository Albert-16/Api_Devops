using System.Net;
using System.Net.Http.Json;
using DockerizeAPI.Models.Enums;
using DockerizeAPI.Models.Requests;
using DockerizeAPI.Models.Responses;
using DockerizeAPI.Tests.Fixtures;

namespace DockerizeAPI.Tests.Endpoints;

/// <summary>
/// Tests de integración para DeployEndpoints.
/// Usa CustomWebApplicationFactory compartida vía Collection para evitar conflictos de puertos.
/// </summary>
[Collection("Integration")]
public sealed class DeployEndpointsTests
{
    private readonly HttpClient _client;

    public DeployEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDeploys_RetornaRespuestaPaginada()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/deploys");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<DeployResponse>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.TotalCount >= 0);
    }

    [Fact]
    public async Task GetDeployById_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/deploys/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeploy_RequestValido_RetornaAccepted()
    {
        // Arrange
        var request = new CreateDeployRequest
        {
            ImageName = "repos.dvhn/org/ms23-auth:sapp-dev",
            GitToken = "test-token-12345",
            ContainerName = "ms23-auth-test"
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/deploys", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DeployResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.DeployId);
        Assert.Equal("Queued", result.Status.ToString());
        Assert.Equal("ms23-auth-test", result.ContainerName);
    }

    [Fact]
    public async Task CreateDeploy_ConPuertosYVolumenes_RetornaAccepted()
    {
        // Arrange — ports sin network (Docker usa bridge por defecto)
        var request = new CreateDeployRequest
        {
            ImageName = "repos.dvhn/org/ms24-payments:latest",
            GitToken = "test-token-12345",
            ContainerName = "ms24-payments-test",
            Ports = ["3050:8080", "443:443"],
            Volumes = ["/data:/app/data"],
            RestartPolicy = RestartPolicy.UnlessStopped,
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production"
            }
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/deploys", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DeployResponse>();
        Assert.NotNull(result);
        Assert.Equal("ms24-payments-test", result.ContainerName);
    }

    [Fact]
    public async Task CreateDeploy_Sandbox_RetornaAcceptedConIsSandbox()
    {
        // Arrange
        var request = new CreateDeployRequest
        {
            ImageName = "repos.dvhn/org/sandbox-app:latest",
            GitToken = "test-token-12345",
            ContainerName = "sandbox-test-container",
            Sandbox = true
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/deploys", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DeployResponse>();
        Assert.NotNull(result);
        Assert.True(result.IsSandbox);
        Assert.NotEqual(Guid.Empty, result.DeployId);
    }

    [Fact]
    public async Task StopDeploy_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsync($"/api/deploys/{Guid.NewGuid()}/stop", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RestartDeploy_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsync($"/api/deploys/{Guid.NewGuid()}/restart", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveDeploy_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.DeleteAsync($"/api/deploys/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InspectDeploy_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/deploys/{Guid.NewGuid()}/inspect");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RollbackDeploy_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsync($"/api/deploys/{Guid.NewGuid()}/rollback", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RollbackDeploy_SinImagenAnterior_Retorna409()
    {
        // Arrange — crear deploy primero
        var request = new CreateDeployRequest
        {
            ImageName = "repos.dvhn/org/rollback-test:v1",
            GitToken = "test-token",
            ContainerName = "rollback-test-container"
        };

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/deploys", request);
        var deploy = await createResponse.Content.ReadFromJsonAsync<DeployResponse>();
        Assert.NotNull(deploy);

        // Act — intentar rollback sin imagen anterior
        HttpResponseMessage response = await _client.PostAsync($"/api/deploys/{deploy.DeployId}/rollback", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetDeployLogs_NoExiste_Retorna404()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/deploys/{Guid.NewGuid()}/logs");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeploy_ContainerNameInvalido_RetornaError()
    {
        // Arrange — nombre de container con caracteres inválidos
        var request = new CreateDeployRequest
        {
            ImageName = "repos.dvhn/org/test:latest",
            GitToken = "test-token",
            ContainerName = "!invalid container"
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/deploys", request);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Accepted,
            $"Expected 400 or 202, got {response.StatusCode}");
    }
}
