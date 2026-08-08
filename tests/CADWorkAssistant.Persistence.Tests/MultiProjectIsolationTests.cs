using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;

namespace CADWorkAssistant.Persistence.Tests;

/// <summary>
/// 두 프로젝트에 같은 종류의 데이터를 넣고 서로 섞이지 않는지 검증한다 (Milestone 6 §144) -
/// ProjectId 필터링이 하나라도 빠지면 다른 프로젝트의 산출내역/활동이 보이는 심각한 버그가 된다.
/// </summary>
public sealed class MultiProjectIsolationTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MultiProjectIsolationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task QuantityRecordsAndActivity_DoNotLeakAcrossProjects()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        var projects = new SqliteProjectRepository();
        var quantities = new SqliteQuantityRecordRepository();
        var activity = new SqliteActivityRepository();

        var now = DateTimeOffset.UtcNow;
        var projectA = new Project(Guid.NewGuid().ToString("N"), "A 현장", now, now, now);
        var projectB = new Project(Guid.NewGuid().ToString("N"), "B 현장", now, now, now);
        await projects.InsertAsync(projectA, connection);
        await projects.InsertAsync(projectB, connection);

        await quantities.InsertAsync(
            new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "A-WALL", 1, 10m, "m", "a.dwg", now) { ProjectId = projectA.Id },
            connection);
        await quantities.InsertAsync(
            new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "B-WALL", 1, 20m, "m", "b.dwg", now) { ProjectId = projectB.Id },
            connection);

        await activity.InsertAsync(
            new ActivityRecord(Guid.NewGuid().ToString("N"), projectA.Id, "QuantityAdded", "A 활동", null, now), connection);
        await activity.InsertAsync(
            new ActivityRecord(Guid.NewGuid().ToString("N"), projectB.Id, "QuantityAdded", "B 활동", null, now), connection);

        var recordsA = await quantities.GetByProjectAsync(projectA.Id, connection);
        var recordsB = await quantities.GetByProjectAsync(projectB.Id, connection);
        var activityA = await activity.GetByProjectAsync(projectA.Id, limit: 10, connection);
        var activityB = await activity.GetByProjectAsync(projectB.Id, limit: 10, connection);

        Assert.Single(recordsA);
        Assert.Equal(10m, recordsA[0].Value);
        Assert.Single(recordsB);
        Assert.Equal(20m, recordsB[0].Value);

        Assert.Single(activityA);
        Assert.Equal("A 활동", activityA[0].Title);
        Assert.Single(activityB);
        Assert.Equal("B 활동", activityB[0].Title);
    }

    [Fact]
    public async Task RecentMeasurement_UpsertKey_IsScopedPerProject()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        var projects = new SqliteProjectRepository();
        var recentMeasurements = new SqliteRecentMeasurementRepository();

        var now = DateTimeOffset.UtcNow;
        var projectA = new Project(Guid.NewGuid().ToString("N"), "A 현장", now, now, now);
        var projectB = new Project(Guid.NewGuid().ToString("N"), "B 현장", now, now, now);
        await projects.InsertAsync(projectA, connection);
        await projects.InsertAsync(projectB, connection);

        // 같은 MeasurementType("VerticalArea")이라도 ProjectId가 다르면 서로 다른 행이어야 한다 -
        // UNIQUE(ProjectId, MeasurementType)이 ProjectId 없이 MeasurementType만으로 걸려 있으면
        // 이 테스트가 실패한다.
        await recentMeasurements.UpsertAsync(
            new RecentMeasurement(Guid.NewGuid().ToString("N"), projectA.Id, "VerticalArea", 100m, "m²", null, null, now), connection);
        await recentMeasurements.UpsertAsync(
            new RecentMeasurement(Guid.NewGuid().ToString("N"), projectB.Id, "VerticalArea", 200m, "m²", null, null, now), connection);

        var resultsA = await recentMeasurements.GetByProjectAsync(projectA.Id, connection);
        var resultsB = await recentMeasurements.GetByProjectAsync(projectB.Id, connection);

        Assert.Equal(100m, Assert.Single(resultsA).Value);
        Assert.Equal(200m, Assert.Single(resultsB).Value);
    }
}
