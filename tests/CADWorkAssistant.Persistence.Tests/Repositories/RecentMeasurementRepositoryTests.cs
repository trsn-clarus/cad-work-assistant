using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class RecentMeasurementRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _projects = new SqliteProjectRepository();
    private readonly IRecentMeasurementRepository _recentMeasurements = new SqliteRecentMeasurementRepository();

    public RecentMeasurementRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpsertAsync_SameProjectAndType_KeepsOnlyLatestValue()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        await _recentMeasurements.UpsertAsync(
            new RecentMeasurement(Guid.NewGuid().ToString("N"), project.Id, "VerticalArea", 100.0m, "m²", "old.dwg", null, DateTimeOffset.UtcNow.AddMinutes(-5)),
            connection);
        await _recentMeasurements.UpsertAsync(
            new RecentMeasurement(Guid.NewGuid().ToString("N"), project.Id, "VerticalArea", 255.941m, "m²", "new.dwg", new[] { "AB12" }, DateTimeOffset.UtcNow),
            connection);

        var results = await _recentMeasurements.GetByProjectAsync(project.Id, connection);

        var only = Assert.Single(results);
        Assert.Equal(255.941m, only.Value);
        Assert.Equal("new.dwg", only.SourceDrawing);
        Assert.Equal(new[] { "AB12" }, only.ObjectHandles);
    }

    [Fact]
    public async Task UpsertAsync_DifferentTypes_KeepsSeparateRows()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        await _recentMeasurements.UpsertAsync(
            new RecentMeasurement(Guid.NewGuid().ToString("N"), project.Id, "VerticalArea", 10m, "m²", null, null, DateTimeOffset.UtcNow),
            connection);
        await _recentMeasurements.UpsertAsync(
            new RecentMeasurement(Guid.NewGuid().ToString("N"), project.Id, "Parapet", 20m, "m", null, null, DateTimeOffset.UtcNow),
            connection);

        var results = await _recentMeasurements.GetByProjectAsync(project.Id, connection);

        Assert.Equal(2, results.Count);
    }

    private async Task<Project> InsertProjectAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "최근 측정값 테스트", now, now, now);
        await _projects.InsertAsync(project, connection);
        return project;
    }
}
