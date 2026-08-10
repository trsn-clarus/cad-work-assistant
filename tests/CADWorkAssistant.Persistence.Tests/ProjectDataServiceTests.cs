using System;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests;

/// <summary>
/// Milestone 6 §45-46이 요구하는 유일한 교차 테이블 원자성: QuantityRecord+ActivityRecord 저장은
/// 같이 성공하거나 같이 실패해야 한다. 성공 경로뿐 아니라 강제로 실패(FK 위반)를 일으켜 롤백이
/// 실제로 두 테이블 모두에 적용되는지 검증한다 - 그렇지 않으면 "기록은 있는데 활동 이력만 없는"
/// 조용한 데이터 불일치가 생길 수 있다.
/// </summary>
public sealed class ProjectDataServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public ProjectDataServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddQuantityRecordWithActivityAsync_BothSucceed_BothArePersisted()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var project = await CreateProjectAsync(database, service);

        var record = new QuantityRecord(
            Guid.NewGuid().ToString("N"), "Length", "A-DOOR", 1, 12.5m, "m", "test.dwg", DateTimeOffset.UtcNow)
        {
            ProjectId = project.Id,
        };
        var activity = new ActivityRecord(
            Guid.NewGuid().ToString("N"), project.Id, "QuantityAdded", "길이 산출 추가", null, DateTimeOffset.UtcNow);

        await service.AddQuantityRecordWithActivityAsync(record, activity);

        using var connection = database.OpenConnection();
        var records = await service.QuantityRecords.GetByProjectAsync(project.Id, connection);
        var activities = await service.Activity.GetByProjectAsync(project.Id, limit: 10, connection);

        Assert.Single(records);
        // CreateProjectAsync가 이미 활동 1건을 남기므로 +1건 = 2건이어야 한다.
        Assert.Equal(2, activities.Count);
    }

    [Fact]
    public async Task AddQuantityRecordWithActivityAsync_QuantityInsertFails_ActivityIsRolledBackToo()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);

        // 존재하지 않는 ProjectId를 그대로 둬서 FK 제약 위반으로 QuantityRecord INSERT가 실패하게
        // 만든다 - 그 뒤에 오는 ActivityRecord INSERT도 롤백되어야 한다.
        var missingProjectId = Guid.NewGuid().ToString("N");
        var record = new QuantityRecord(
            Guid.NewGuid().ToString("N"), "Length", "A-DOOR", 1, 1m, "m", "test.dwg", DateTimeOffset.UtcNow)
        {
            ProjectId = missingProjectId,
        };
        var activity = new ActivityRecord(
            Guid.NewGuid().ToString("N"), missingProjectId, "QuantityAdded", "실패해야 함", null, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<SqliteException>(() => service.AddQuantityRecordWithActivityAsync(record, activity));

        using var connection = database.OpenConnection();
        var activities = await service.Activity.GetByProjectAsync(missingProjectId, limit: 10, connection);
        Assert.Empty(activities);
    }

    [Fact]
    public async Task CreateProjectWithActivityAsync_PersistsBothInOneTransaction()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var project = await CreateProjectAsync(database, service);

        using var connection = database.OpenConnection();
        var found = await service.Projects.FindByIdAsync(project.Id, connection);
        var activities = await service.Activity.GetByProjectAsync(project.Id, limit: 10, connection);

        Assert.NotNull(found);
        Assert.Single(activities);
        Assert.Equal("ProjectCreated", activities[0].ActivityType);
    }

    [Fact]
    public async Task SaveReviewAsync_WithActivity_PersistsBothInOneTransaction()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var project = await CreateProjectAsync(database, service);
        var record = await AddQuantityRecordAsync(database, service, project.Id);

        var review = new QuantityReview(Guid.NewGuid().ToString("N"), project.Id, record.Id,
            QuantityReviewStatus.Verified, "확인 완료", DateTimeOffset.UtcNow);
        var activity = new ActivityRecord(Guid.NewGuid().ToString("N"), project.Id, "QuantityVerified", "산출내역 검토 완료", null, DateTimeOffset.UtcNow);

        await service.SaveReviewAsync(review, activity);

        using var connection = database.OpenConnection();
        var reviews = await service.QuantityReviews.GetByProjectAsync(project.Id, connection);
        var activities = await service.Activity.GetByProjectAsync(project.Id, limit: 10, connection);

        Assert.Single(reviews);
        Assert.Contains(activities, a => a.ActivityType == "QuantityVerified");
    }

    [Fact]
    public async Task SaveReviewAsync_WithoutActivity_PersistsReviewOnly()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var project = await CreateProjectAsync(database, service);
        var record = await AddQuantityRecordAsync(database, service, project.Id);

        var review = new QuantityReview(Guid.NewGuid().ToString("N"), project.Id, record.Id, QuantityReviewStatus.NeedsReview, null, null);
        await service.SaveReviewAsync(review, activity: null);

        using var connection = database.OpenConnection();
        var reviews = await service.QuantityReviews.GetByProjectAsync(project.Id, connection);
        var activities = await service.Activity.GetByProjectAsync(project.Id, limit: 10, connection);

        Assert.Single(reviews);
        // CreateProjectAsync가 남긴 활동 1건만 있어야 한다 - 자동 재검산처럼 매번 Activity를 남기지 않는다(§54).
        Assert.Single(activities);
    }

    [Fact]
    public async Task RelinkDrawingFileAsync_WithActivity_PersistsBothInOneTransaction()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var project = await CreateProjectAsync(database, service);

        var now = DateTimeOffset.UtcNow;
        var drawingFile = new DrawingFile(Guid.NewGuid().ToString("N"), project.Id, "Old.dwg", @"C:\old\Old.dwg", null, now, now, isMissing: true);
        using (var setupConnection = database.OpenConnection())
        {
            await service.DrawingFiles.UpsertAsync(drawingFile, setupConnection);
        }

        var activity = new ActivityRecord(Guid.NewGuid().ToString("N"), project.Id, "DrawingFileRelinked", "도면 파일 다시 연결", "New.dwg", now);
        await service.RelinkDrawingFileAsync(drawingFile.Id, @"C:\new\New.dwg", "New.dwg", "mm", now.AddMinutes(1), activity);

        using var connection = database.OpenConnection();
        var found = (await service.DrawingFiles.GetByProjectAsync(project.Id, connection)).Single();
        var activities = await service.Activity.GetByProjectAsync(project.Id, limit: 10, connection);

        Assert.Equal(@"C:\new\New.dwg", found.FullPath);
        Assert.False(found.IsMissing);
        // CreateProjectAsync가 이미 활동 1건을 남기므로 +1건 = 2건이어야 한다.
        Assert.Equal(2, activities.Count);
        Assert.Contains(activities, a => a.ActivityType == "DrawingFileRelinked");
    }

    [Fact]
    public async Task RelinkDrawingFileAsync_ActivityInsertFails_DrawingFileUpdateIsRolledBackToo()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var project = await CreateProjectAsync(database, service);

        var now = DateTimeOffset.UtcNow;
        var drawingFile = new DrawingFile(Guid.NewGuid().ToString("N"), project.Id, "Old.dwg", @"C:\old\Old.dwg", null, now, now);
        using (var setupConnection = database.OpenConnection())
        {
            await service.DrawingFiles.UpsertAsync(drawingFile, setupConnection);
        }

        // 존재하지 않는 ProjectId를 써서 ActivityRecord INSERT가 FK 위반으로 실패하게 만든다 - 그 앞의
        // DrawingFile UPDATE도 롤백되어야 한다(§53 all-or-nothing과 같은 기준).
        var missingProjectId = Guid.NewGuid().ToString("N");
        var activity = new ActivityRecord(Guid.NewGuid().ToString("N"), missingProjectId, "DrawingFileRelinked", "실패해야 함", null, now);

        await Assert.ThrowsAsync<SqliteException>(() =>
            service.RelinkDrawingFileAsync(drawingFile.Id, @"C:\new\New.dwg", "New.dwg", null, now.AddMinutes(1), activity));

        using var connection = database.OpenConnection();
        var found = (await service.DrawingFiles.GetByProjectAsync(project.Id, connection)).Single();
        Assert.Equal(@"C:\old\Old.dwg", found.FullPath);
    }

    private static async Task<QuantityRecord> AddQuantityRecordAsync(CadWorkAssistantDatabase database, ProjectDataService service, string projectId)
    {
        var record = new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "A-WALL", 1, 10m, "m", "test.dwg", DateTimeOffset.UtcNow)
        {
            ProjectId = projectId,
        };
        using var connection = database.OpenConnection();
        await service.QuantityRecords.InsertAsync(record, connection);
        return record;
    }

    private static async Task<Project> CreateProjectAsync(CadWorkAssistantDatabase database, ProjectDataService service)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "트랜잭션 테스트 프로젝트", now, now, now);
        var activity = new ActivityRecord(
            Guid.NewGuid().ToString("N"), project.Id, "ProjectCreated", "프로젝트 생성", null, now);

        await service.CreateProjectWithActivityAsync(project, activity);
        return project;
    }
}
