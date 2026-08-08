using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class QuantityReviewRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _projects = new SqliteProjectRepository();
    private readonly IQuantityRecordRepository _quantityRecords = new SqliteQuantityRecordRepository();
    private readonly IQuantityReviewRepository _reviews = new SqliteQuantityReviewRepository();

    public QuantityReviewRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpsertAsync_ThenGetByProjectAsync_RoundTripsStatusAndNote()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var (project, record) = await InsertProjectAndRecordAsync(connection);

        var review = new QuantityReview(Guid.NewGuid().ToString("N"), project.Id, record.Id,
            QuantityReviewStatus.Verified, "ㄱ자 평면이라 둘레가 길지만 정상", DateTimeOffset.UtcNow);

        await _reviews.UpsertAsync(review, connection);
        var found = (await _reviews.GetByProjectAsync(project.Id, connection)).Single();

        Assert.Equal(QuantityReviewStatus.Verified, found.Status);
        Assert.Equal("ㄱ자 평면이라 둘레가 길지만 정상", found.Note);
        Assert.NotNull(found.ReviewedAt);
    }

    [Fact]
    public async Task UpsertAsync_SameQuantityRecordTwice_KeepsOnlyLatestStatus()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var (project, record) = await InsertProjectAndRecordAsync(connection);

        await _reviews.UpsertAsync(
            new QuantityReview(Guid.NewGuid().ToString("N"), project.Id, record.Id, QuantityReviewStatus.NeedsReview, null, null),
            connection);
        await _reviews.UpsertAsync(
            new QuantityReview(Guid.NewGuid().ToString("N"), project.Id, record.Id, QuantityReviewStatus.Verified, "확인 완료", DateTimeOffset.UtcNow),
            connection);

        var results = await _reviews.GetByProjectAsync(project.Id, connection);

        var only = Assert.Single(results);
        Assert.Equal(QuantityReviewStatus.Verified, only.Status);
        Assert.Equal("확인 완료", only.Note);
    }

    [Fact]
    public async Task UpsertAsync_DefaultUnreviewedWithoutNote_RoundTripsNullNote()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var (project, record) = await InsertProjectAndRecordAsync(connection);

        await _reviews.UpsertAsync(
            new QuantityReview(Guid.NewGuid().ToString("N"), project.Id, record.Id, QuantityReviewStatus.Unreviewed, null, null),
            connection);

        var found = (await _reviews.GetByProjectAsync(project.Id, connection)).Single();

        Assert.Equal(QuantityReviewStatus.Unreviewed, found.Status);
        Assert.Null(found.Note);
        Assert.Null(found.ReviewedAt);
    }

    private async Task<(Project Project, QuantityRecord Record)> InsertProjectAndRecordAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "검토 테스트 프로젝트", now, now, now);
        await _projects.InsertAsync(project, connection);

        var record = new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "A-WALL", 1, 10m, "m", "test.dwg", now)
        {
            ProjectId = project.Id,
        };
        await _quantityRecords.InsertAsync(record, connection);

        return (project, record);
    }
}
