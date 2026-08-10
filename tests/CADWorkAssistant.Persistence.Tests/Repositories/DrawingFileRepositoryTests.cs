using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class DrawingFileRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _projects = new SqliteProjectRepository();
    private readonly IDrawingFileRepository _drawingFiles = new SqliteDrawingFileRepository();

    public DrawingFileRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpsertAsync_SamePathTwice_DoesNotCreateDuplicateRow()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        var firstSeen = DateTimeOffset.UtcNow.AddHours(-1);
        var secondSeen = DateTimeOffset.UtcNow;

        await _drawingFiles.UpsertAsync(
            new DrawingFile(Guid.NewGuid().ToString("N"), project.Id, "건축평면도.dwg", @"C:\drawings\건축평면도.dwg", "mm", firstSeen, firstSeen),
            connection);
        await _drawingFiles.UpsertAsync(
            new DrawingFile(Guid.NewGuid().ToString("N"), project.Id, "건축평면도.dwg", @"C:\drawings\건축평면도.dwg", "mm", secondSeen, secondSeen),
            connection);

        var files = await _drawingFiles.GetByProjectAsync(project.Id, connection);

        var file = Assert.Single(files);
        Assert.True(Math.Abs((file.LastSeenAt - secondSeen).TotalSeconds) < 1);
    }

    [Fact]
    public async Task UpsertAsync_IsMissingFlag_RoundTrips()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);
        var now = DateTimeOffset.UtcNow;

        await _drawingFiles.UpsertAsync(
            new DrawingFile(Guid.NewGuid().ToString("N"), project.Id, "이동됨.dwg", @"C:\drawings\이동됨.dwg", null, now, now, isMissing: true),
            connection);

        var found = (await _drawingFiles.GetByProjectAsync(project.Id, connection)).Single();
        Assert.True(found.IsMissing);
    }

    [Fact]
    public async Task RelinkAsync_UpdatesPathAndClearsMissingFlag()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);
        var now = DateTimeOffset.UtcNow;

        var drawingFile = new DrawingFile(
            Guid.NewGuid().ToString("N"), project.Id, "School_A.dwg", @"C:\old\School_A.dwg", "mm", now, now, isMissing: true);
        await _drawingFiles.UpsertAsync(drawingFile, connection);

        var relinkedAt = now.AddMinutes(5);
        await _drawingFiles.RelinkAsync(drawingFile.Id, @"C:\new\School_Final.dwg", "School_Final.dwg", "mm", relinkedAt, connection);

        var found = (await _drawingFiles.GetByProjectAsync(project.Id, connection)).Single();
        Assert.Equal(@"C:\new\School_Final.dwg", found.FullPath);
        Assert.Equal("School_Final.dwg", found.FileName);
        Assert.False(found.IsMissing);
        Assert.True(Math.Abs((found.LastSeenAt - relinkedAt).TotalSeconds) < 1);
    }

    [Fact]
    public async Task GetCountsByProjectAsync_ReturnsCorrectCountsPerProject()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var projectA = await InsertProjectAsync(connection);
        var projectB = await InsertProjectAsync(connection);
        var now = DateTimeOffset.UtcNow;

        await _drawingFiles.UpsertAsync(new DrawingFile(Guid.NewGuid().ToString("N"), projectA.Id, "A1.dwg", @"C:\a\A1.dwg", null, now, now), connection);
        await _drawingFiles.UpsertAsync(new DrawingFile(Guid.NewGuid().ToString("N"), projectA.Id, "A2.dwg", @"C:\a\A2.dwg", null, now, now), connection);
        await _drawingFiles.UpsertAsync(new DrawingFile(Guid.NewGuid().ToString("N"), projectB.Id, "B1.dwg", @"C:\b\B1.dwg", null, now, now), connection);

        var counts = await _drawingFiles.GetCountsByProjectAsync(connection);

        Assert.Equal(2, counts[projectA.Id]);
        Assert.Equal(1, counts[projectB.Id]);
    }

    private async Task<Project> InsertProjectAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "도면 참조 테스트", now, now, now);
        await _projects.InsertAsync(project, connection);
        return project;
    }
}
