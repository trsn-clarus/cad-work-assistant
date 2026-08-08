using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class ExportRecordRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _projects = new SqliteProjectRepository();
    private readonly IExportRecordRepository _exportRecords = new SqliteExportRecordRepository();

    public ExportRecordRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InsertAsync_ThenGetByProjectAsync_RoundTripsFields()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        var record = new ExportRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            sourceDrawing: "OO학교_건축.dwg",
            targetFile: @"C:\exports\OO학교_건축_실내마감표.dwg",
            objectCount: 42,
            description: "실내마감표만 추출",
            createdAt: DateTimeOffset.UtcNow);

        await _exportRecords.InsertAsync(record, connection);
        var found = (await _exportRecords.GetByProjectAsync(project.Id, connection)).Single();

        Assert.Equal(record.SourceDrawing, found.SourceDrawing);
        Assert.Equal(record.TargetFile, found.TargetFile);
        Assert.Equal(record.ObjectCount, found.ObjectCount);
        Assert.Equal(record.Description, found.Description);
    }

    private async Task<Project> InsertProjectAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "Export 테스트", now, now, now);
        await _projects.InsertAsync(project, connection);
        return project;
    }
}
