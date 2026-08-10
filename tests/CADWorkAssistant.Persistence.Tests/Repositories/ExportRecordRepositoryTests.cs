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
        Assert.Equal(ExportTypes.DwgSelection, found.ExportType);
    }

    [Fact]
    public async Task InsertAsync_ExcelQuantityExportType_RoundTrips()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        var record = new ExportRecord(
            id: Guid.NewGuid().ToString("N"),
            projectId: project.Id,
            sourceDrawing: null,
            targetFile: @"C:\exports\OO초등학교_옥상방수_수량산출서_20260809.xlsx",
            objectCount: 42,
            description: "전체 42건",
            createdAt: DateTimeOffset.UtcNow,
            exportType: ExportTypes.ExcelQuantity);

        await _exportRecords.InsertAsync(record, connection);
        var found = (await _exportRecords.GetByProjectAsync(project.Id, connection)).Single();

        Assert.Equal(ExportTypes.ExcelQuantity, found.ExportType);
        Assert.Null(found.SourceDrawing);
    }

    [Fact]
    public async Task GetByProjectAsync_WithLimit_ReturnsOnlyMostRecent()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);
        var baseTime = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            var record = new ExportRecord(
                id: Guid.NewGuid().ToString("N"),
                projectId: project.Id,
                sourceDrawing: null,
                targetFile: $@"C:\exports\output_{i}.xlsx",
                objectCount: i,
                description: null,
                createdAt: baseTime.AddMinutes(i),
                exportType: ExportTypes.ExcelQuantity);
            await _exportRecords.InsertAsync(record, connection);
        }

        var page = await _exportRecords.GetByProjectAsync(project.Id, limit: 2, connection);

        Assert.Equal(2, page.Count);
        // CreatedAt DESC이므로 가장 최근에 넣은 output_4가 먼저 와야 한다.
        Assert.Equal(@"C:\exports\output_4.xlsx", page[0].TargetFile);
        Assert.Equal(@"C:\exports\output_3.xlsx", page[1].TargetFile);
    }

    private async Task<Project> InsertProjectAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "Export 테스트", now, now, now);
        await _projects.InsertAsync(project, connection);
        return project;
    }
}
