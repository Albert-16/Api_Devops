using DockerizeAPI.Data;
using DockerizeAPI.Models.Entities;
using DockerizeAPI.Models.Enums;

namespace DockerizeAPI.Tests.Services;

/// <summary>
/// Tests para BuildStore.
/// Verifica operaciones CRUD y paginación del almacenamiento en memoria.
/// </summary>
public sealed class BuildStoreTests
{
    private readonly BuildStore _store = new();

    private static BuildRecord CreateBuild(
        BuildStatus status = BuildStatus.Queued,
        string branch = "main",
        string repoUrl = "https://repos.dvhn/org/repo.git")
    {
        return new BuildRecord
        {
            Id = Guid.NewGuid(),
            RepositoryUrl = repoUrl,
            Branch = branch,
            GitToken = "test-token",
            ImageName = "test-image",
            ImageTag = "latest",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void AddBuild_AgregaBuildExitosamente()
    {
        // Arrange
        BuildRecord build = CreateBuild();

        // Act
        bool result = _store.AddBuild(build);

        // Assert
        Assert.True(result);
        Assert.Equal(1, _store.Count);
    }

    [Fact]
    public void AddBuild_DuplicadoRetornaFalse()
    {
        // Arrange
        BuildRecord build = CreateBuild();
        _store.AddBuild(build);

        // Act
        bool result = _store.AddBuild(build);

        // Assert
        Assert.False(result);
        Assert.Equal(1, _store.Count);
    }

    [Fact]
    public void GetBuild_ExistenteRetornaBuild()
    {
        // Arrange
        BuildRecord build = CreateBuild();
        _store.AddBuild(build);

        // Act
        BuildRecord? result = _store.GetBuild(build.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(build.Id, result.Id);
    }

    [Fact]
    public void GetBuild_NoExistenteRetornaNull()
    {
        // Act
        BuildRecord? result = _store.GetBuild(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void UpdateBuild_ModificaEstado()
    {
        // Arrange
        BuildRecord build = CreateBuild();
        _store.AddBuild(build);

        // Act
        bool updated = _store.UpdateBuild(build.Id, b => b.Status = BuildStatus.Building);

        // Assert
        Assert.True(updated);
        BuildRecord? result = _store.GetBuild(build.Id);
        Assert.Equal(BuildStatus.Building, result!.Status);
    }

    [Fact]
    public void UpdateBuild_NoExistenteRetornaFalse()
    {
        // Act
        bool result = _store.UpdateBuild(Guid.NewGuid(), _ => { });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetBuilds_PaginacionCorrecta()
    {
        // Arrange
        for (int i = 0; i < 25; i++)
        {
            _store.AddBuild(CreateBuild());
        }

        // Act
        var (items, totalCount) = _store.GetBuilds(page: 2, pageSize: 10);

        // Assert
        Assert.Equal(25, totalCount);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public void GetBuilds_FiltroPorStatus()
    {
        // Arrange
        _store.AddBuild(CreateBuild(BuildStatus.Queued));
        _store.AddBuild(CreateBuild(BuildStatus.Completed));
        _store.AddBuild(CreateBuild(BuildStatus.Failed));
        _store.AddBuild(CreateBuild(BuildStatus.Completed));

        // Act
        var (items, totalCount) = _store.GetBuilds(status: BuildStatus.Completed);

        // Assert
        Assert.Equal(2, totalCount);
        Assert.All(items, b => Assert.Equal(BuildStatus.Completed, b.Status));
    }

    [Fact]
    public void GetBuilds_FiltroPorBranch()
    {
        // Arrange
        _store.AddBuild(CreateBuild(branch: "sapp-dev"));
        _store.AddBuild(CreateBuild(branch: "sapp-uat"));
        _store.AddBuild(CreateBuild(branch: "sapp-dev"));

        // Act
        var (items, totalCount) = _store.GetBuilds(branch: "sapp-dev");

        // Assert
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public void AddLog_YGetLogs_RetornaOrdenCronologico()
    {
        // Arrange
        Guid buildId = Guid.NewGuid();
        _store.AddLog(buildId, new BuildLog
        {
            BuildRecordId = buildId,
            Message = "Segundo",
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(1)
        });
        _store.AddLog(buildId, new BuildLog
        {
            BuildRecordId = buildId,
            Message = "Primero",
            Timestamp = DateTimeOffset.UtcNow
        });

        // Act
        IReadOnlyList<BuildLog> logs = _store.GetLogs(buildId);

        // Assert
        Assert.Equal(2, logs.Count);
        Assert.Equal("Primero", logs[0].Message);
        Assert.Equal("Segundo", logs[1].Message);
    }

    [Fact]
    public void GetLogs_SinLogs_RetornaListaVacia()
    {
        // Act
        IReadOnlyList<BuildLog> logs = _store.GetLogs(Guid.NewGuid());

        // Assert
        Assert.Empty(logs);
    }
}
