using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class ActivityRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _projects = new SqliteProjectRepository();
    private readonly IActivityRepository _activity = new SqliteActivityRepository();

    public ActivityRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByProjectAsync_OrdersByCreatedAtDescending()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        var baseline = DateTimeOffset.UtcNow;
        var first = new ActivityRecord(Guid.NewGuid().ToString("N"), project.Id, "QuantityAdded", "첫 번째", null, baseline.AddMinutes(-10));
        var second = new ActivityRecord(Guid.NewGuid().ToString("N"), project.Id, "QuantityAdded", "두 번째", null, baseline.AddMinutes(-5));
        var third = new ActivityRecord(Guid.NewGuid().ToString("N"), project.Id, "ExportCompleted", "세 번째", "메타 설명", baseline, "{\"objectCount\":12}");

        await _activity.InsertAsync(first, connection);
        await _activity.InsertAsync(second, connection);
        await _activity.InsertAsync(third, connection);

        var results = await _activity.GetByProjectAsync(project.Id, limit: 10, connection);

        Assert.Equal(new[] { "세 번째", "두 번째", "첫 번째" }, results.Select(a => a.Title).ToArray());
        Assert.Equal("메타 설명", results[0].Description);
        Assert.Equal("{\"objectCount\":12}", results[0].MetadataJson);
    }

    [Fact]
    public async Task GetByProjectAsync_RespectsLimit()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        for (var i = 0; i < 5; i++)
        {
            await _activity.InsertAsync(
                new ActivityRecord(Guid.NewGuid().ToString("N"), project.Id, "QuantityAdded", $"항목{i}", null, DateTimeOffset.UtcNow.AddSeconds(i)),
                connection);
        }

        var results = await _activity.GetByProjectAsync(project.Id, limit: 2, connection);

        Assert.Equal(2, results.Count);
    }

    private async Task<Project> InsertProjectAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "활동 테스트 프로젝트", now, now, now);
        await _projects.InsertAsync(project, connection);
        return project;
    }
}
