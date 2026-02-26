using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para DeployStore.
/// Verifica operaciones CRUD, paginación, filtros y búsqueda por nombre de container.
/// </summary>
public sealed class DeployStoreTests
{
    private readonly DeployStore _store = new();

    private static DeployRecord CreateDeploy(
        DeployStatus status = DeployStatus.Queued,
        string containerName = "test-container",
        string imageName = "repos.dvhn/org/myapp:latest")
    {
        return new DeployRecord
        {
            Id = Guid.NewGuid(),
            ImageName = imageName,
            ContainerName = containerName,
            GitToken = "test-token",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void AddDeploy_AgregaDeployExitosamente()
    {
        // Arrange
        DeployRecord deploy = CreateDeploy();

        // Act
        bool result = _store.AddDeploy(deploy);

        // Assert
        Assert.True(result);
        Assert.Equal(1, _store.Count);
    }

    [Fact]
    public void AddDeploy_DuplicadoRetornaFalse()
    {
        // Arrange
        DeployRecord deploy = CreateDeploy();
        _store.AddDeploy(deploy);

        // Act
        bool result = _store.AddDeploy(deploy);

        // Assert
        Assert.False(result);
        Assert.Equal(1, _store.Count);
    }

    [Fact]
    public void GetDeploy_ExistenteRetornaDeploy()
    {
        // Arrange
        DeployRecord deploy = CreateDeploy();
        _store.AddDeploy(deploy);

        // Act
        DeployRecord? result = _store.GetDeploy(deploy.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(deploy.Id, result.Id);
    }

    [Fact]
    public void GetDeploy_NoExistenteRetornaNull()
    {
        // Act
        DeployRecord? result = _store.GetDeploy(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void UpdateDeploy_ModificaEstado()
    {
        // Arrange
        DeployRecord deploy = CreateDeploy();
        _store.AddDeploy(deploy);

        // Act
        bool updated = _store.UpdateDeploy(deploy.Id, d => d.Status = DeployStatus.Running);

        // Assert
        Assert.True(updated);
        DeployRecord? result = _store.GetDeploy(deploy.Id);
        Assert.Equal(DeployStatus.Running, result!.Status);
    }

    [Fact]
    public void UpdateDeploy_NoExistenteRetornaFalse()
    {
        // Act
        bool result = _store.UpdateDeploy(Guid.NewGuid(), _ => { });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetDeploys_PaginacionCorrecta()
    {
        // Arrange
        for (int i = 0; i < 25; i++)
        {
            _store.AddDeploy(CreateDeploy());
        }

        // Act
        var (items, totalCount) = _store.GetDeploys(page: 2, pageSize: 10);

        // Assert
        Assert.Equal(25, totalCount);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public void GetDeploys_FiltroPorStatus()
    {
        // Arrange
        _store.AddDeploy(CreateDeploy(DeployStatus.Queued));
        _store.AddDeploy(CreateDeploy(DeployStatus.Running));
        _store.AddDeploy(CreateDeploy(DeployStatus.Failed));
        _store.AddDeploy(CreateDeploy(DeployStatus.Running));

        // Act
        var (items, totalCount) = _store.GetDeploys(status: DeployStatus.Running);

        // Assert
        Assert.Equal(2, totalCount);
        Assert.All(items, d => Assert.Equal(DeployStatus.Running, d.Status));
    }

    [Fact]
    public void GetDeploys_FiltroPorContainerName()
    {
        // Arrange
        _store.AddDeploy(CreateDeploy(containerName: "ms23-auth"));
        _store.AddDeploy(CreateDeploy(containerName: "ms24-payments"));
        _store.AddDeploy(CreateDeploy(containerName: "ms23-auth-dev"));

        // Act
        var (items, totalCount) = _store.GetDeploys(containerName: "ms23-auth");

        // Assert
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public void GetDeploys_FiltroPorImageName()
    {
        // Arrange
        _store.AddDeploy(CreateDeploy(imageName: "repos.dvhn/org/ms23:latest"));
        _store.AddDeploy(CreateDeploy(imageName: "repos.dvhn/org/ms24:latest"));
        _store.AddDeploy(CreateDeploy(imageName: "repos.dvhn/org/ms23:dev"));

        // Act
        var (items, totalCount) = _store.GetDeploys(imageName: "ms23");

        // Assert
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public void FindByContainerName_RetornaDeployActivo()
    {
        // Arrange
        DeployRecord running = CreateDeploy(DeployStatus.Running, "my-container");
        DeployRecord failed = CreateDeploy(DeployStatus.Failed, "my-container");
        _store.AddDeploy(running);
        _store.AddDeploy(failed);

        // Act
        DeployRecord? result = _store.FindByContainerName("my-container");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DeployStatus.Running, result.Status);
    }

    [Fact]
    public void FindByContainerName_NoActivoRetornaNull()
    {
        // Arrange
        _store.AddDeploy(CreateDeploy(DeployStatus.Failed, "my-container"));
        _store.AddDeploy(CreateDeploy(DeployStatus.Stopped, "my-container"));

        // Act
        DeployRecord? result = _store.FindByContainerName("my-container");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AddLog_YGetLogs_RetornaOrdenCronologico()
    {
        // Arrange
        Guid deployId = Guid.NewGuid();
        _store.AddLog(deployId, new DeployLog
        {
            DeployRecordId = deployId,
            Message = "Segundo",
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(1)
        });
        _store.AddLog(deployId, new DeployLog
        {
            DeployRecordId = deployId,
            Message = "Primero",
            Timestamp = DateTimeOffset.UtcNow
        });

        // Act
        IReadOnlyList<DeployLog> logs = _store.GetLogs(deployId);

        // Assert
        Assert.Equal(2, logs.Count);
        Assert.Equal("Primero", logs[0].Message);
        Assert.Equal("Segundo", logs[1].Message);
    }

    [Fact]
    public void GetLogs_SinLogs_RetornaListaVacia()
    {
        // Act
        IReadOnlyList<DeployLog> logs = _store.GetLogs(Guid.NewGuid());

        // Assert
        Assert.Empty(logs);
    }
}
