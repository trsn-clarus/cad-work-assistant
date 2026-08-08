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

    private async Task<Project> InsertProjectAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "도면 참조 테스트", now, now, now);
        await _projects.InsertAsync(project, connection);
        return project;
    }
}
