using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Persistence.Repositories;

namespace CADWorkAssistant.Persistence.Tests;

/// <summary>
/// Milestone 7 §98-101: 검산 결과/검토 상태가 앱 재시작 후에도 정확히 복원되고, 두 Project 사이에
/// 절대 섞이지 않는지 검증한다. <see cref="AppRestartSimulationTests"/>/<see cref="MultiProjectIsolationTests"/>와
/// 같은 검증 방식(:memory: 아닌 실제 파일, 완전히 새로운 CadWorkAssistantDatabase 인스턴스)을
/// Verification/Review 테이블에 그대로 적용한다.
/// </summary>
public sealed class VerificationAndReviewRestartTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public VerificationAndReviewRestartTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task VerificationSnapshotAndReview_SurviveAppRestart()
    {
        var projectId = Guid.NewGuid().ToString("N");
        var recordId = Guid.NewGuid().ToString("N");

        // "앱 실행 1회차"
        {
            var database = _fixture.CreateDatabase();
            var service = new ProjectDataService(database);
            var now = DateTimeOffset.UtcNow;

            using var connection = database.OpenConnection();
            await service.Projects.InsertAsync(new Project(projectId, "재시작 검증 프로젝트", now, now, now), connection);
            await service.QuantityRecords.InsertAsync(
                new QuantityRecord(recordId, "Parapet", "A-WALL", 1, 69.0537m, "m²", "restart.dwg", now) { ProjectId = projectId },
                connection);

            await service.QuantityVerifications.UpsertAsync(
                new QuantityVerificationSnapshot(Guid.NewGuid().ToString("N"), projectId, recordId,
                    VerificationSeverity.Review, QuantityVerificationService.CurrentRuleSetVersion, now, "[{\"RuleId\":\"FormulaRecompute\"}]"),
                connection);
            await service.QuantityReviews.UpsertAsync(
                new QuantityReview(Guid.NewGuid().ToString("N"), projectId, recordId, QuantityReviewStatus.Verified, "현장 확인 완료", now),
                connection);
        }

        // "앱 종료 후 재실행" - 완전히 새로운 CadWorkAssistantDatabase 인스턴스, 같은 파일 경로.
        {
            var database = _fixture.CreateDatabase();
            var service = new ProjectDataService(database);
            using var connection = database.OpenConnection();

            var snapshot = (await service.QuantityVerifications.GetByProjectAsync(projectId, connection)).Single();
            var review = (await service.QuantityReviews.GetByProjectAsync(projectId, connection)).Single();

            Assert.Equal(VerificationSeverity.Review, snapshot.OverallSeverity);
            Assert.Equal(QuantityReviewStatus.Verified, review.Status);
            Assert.Equal("현장 확인 완료", review.Note);
        }
    }

    [Fact]
    public async Task VerificationAndReview_DoNotLeakAcrossProjects()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        using var connection = database.OpenConnection();

        var now = DateTimeOffset.UtcNow;
        var projectA = new Project(Guid.NewGuid().ToString("N"), "A 현장", now, now, now);
        var projectB = new Project(Guid.NewGuid().ToString("N"), "B 현장", now, now, now);
        await service.Projects.InsertAsync(projectA, connection);
        await service.Projects.InsertAsync(projectB, connection);

        var recordA = new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "A-WALL", 1, 10m, "m", "a.dwg", now) { ProjectId = projectA.Id };
        var recordB = new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "B-WALL", 1, 20m, "m", "b.dwg", now) { ProjectId = projectB.Id };
        await service.QuantityRecords.InsertAsync(recordA, connection);
        await service.QuantityRecords.InsertAsync(recordB, connection);

        await service.QuantityVerifications.UpsertAsync(
            new QuantityVerificationSnapshot(Guid.NewGuid().ToString("N"), projectA.Id, recordA.Id, VerificationSeverity.Pass, 1, now, "[]"), connection);
        await service.QuantityVerifications.UpsertAsync(
            new QuantityVerificationSnapshot(Guid.NewGuid().ToString("N"), projectB.Id, recordB.Id, VerificationSeverity.Error, 1, now, "[]"), connection);

        await service.QuantityReviews.UpsertAsync(
            new QuantityReview(Guid.NewGuid().ToString("N"), projectA.Id, recordA.Id, QuantityReviewStatus.Verified, null, now), connection);
        await service.QuantityReviews.UpsertAsync(
            new QuantityReview(Guid.NewGuid().ToString("N"), projectB.Id, recordB.Id, QuantityReviewStatus.NeedsReview, null, now), connection);

        var verificationsA = await service.QuantityVerifications.GetByProjectAsync(projectA.Id, connection);
        var verificationsB = await service.QuantityVerifications.GetByProjectAsync(projectB.Id, connection);
        var reviewsA = await service.QuantityReviews.GetByProjectAsync(projectA.Id, connection);
        var reviewsB = await service.QuantityReviews.GetByProjectAsync(projectB.Id, connection);

        Assert.Equal(VerificationSeverity.Pass, Assert.Single(verificationsA).OverallSeverity);
        Assert.Equal(VerificationSeverity.Error, Assert.Single(verificationsB).OverallSeverity);
        Assert.Equal(QuantityReviewStatus.Verified, Assert.Single(reviewsA).Status);
        Assert.Equal(QuantityReviewStatus.NeedsReview, Assert.Single(reviewsB).Status);
    }

    [Fact]
    public async Task DeletingQuantityRecord_CascadesVerificationAndReview()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        using var connection = database.OpenConnection();

        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "삭제 캐스케이드 테스트", now, now, now);
        await service.Projects.InsertAsync(project, connection);
        var record = new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "A-WALL", 1, 10m, "m", "test.dwg", now) { ProjectId = project.Id };
        await service.QuantityRecords.InsertAsync(record, connection);

        await service.QuantityVerifications.UpsertAsync(
            new QuantityVerificationSnapshot(Guid.NewGuid().ToString("N"), project.Id, record.Id, VerificationSeverity.Pass, 1, now, "[]"), connection);
        await service.QuantityReviews.UpsertAsync(
            new QuantityReview(Guid.NewGuid().ToString("N"), project.Id, record.Id, QuantityReviewStatus.Verified, null, now), connection);

        await service.QuantityRecords.DeleteAsync(record.Id, connection);

        var verifications = await service.QuantityVerifications.GetByProjectAsync(project.Id, connection);
        var reviews = await service.QuantityReviews.GetByProjectAsync(project.Id, connection);

        Assert.Empty(verifications);
        Assert.Empty(reviews);
    }
}
