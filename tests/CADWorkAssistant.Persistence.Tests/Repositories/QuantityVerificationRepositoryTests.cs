using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class QuantityVerificationRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _projects = new SqliteProjectRepository();
    private readonly IQuantityRecordRepository _quantityRecords = new SqliteQuantityRecordRepository();
    private readonly IQuantityVerificationRepository _verifications = new SqliteQuantityVerificationRepository();

    public QuantityVerificationRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpsertAsync_ThenGetByProjectAsync_RoundTripsFields()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var (project, record) = await InsertProjectAndRecordAsync(connection);

        var checksJson = "[{\"RuleId\":\"PositiveQuantity\",\"Severity\":\"Pass\"}]";
        var snapshot = new QuantityVerificationSnapshot(
            Guid.NewGuid().ToString("N"), project.Id, record.Id,
            VerificationSeverity.Review, QuantityVerificationService.CurrentRuleSetVersion, DateTimeOffset.UtcNow, checksJson);

        await _verifications.UpsertAsync(snapshot, connection);
        var found = (await _verifications.GetByProjectAsync(project.Id, connection)).Single();

        Assert.Equal(VerificationSeverity.Review, found.OverallSeverity);
        Assert.Equal(QuantityVerificationService.CurrentRuleSetVersion, found.RuleSetVersion);
        Assert.Equal(checksJson, found.ChecksJson);
    }

    [Fact]
    public async Task UpsertAsync_SameQuantityRecordTwice_KeepsOnlyLatestSnapshot()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var (project, record) = await InsertProjectAndRecordAsync(connection);

        await _verifications.UpsertAsync(
            new QuantityVerificationSnapshot(Guid.NewGuid().ToString("N"), project.Id, record.Id,
                VerificationSeverity.Error, 1, DateTimeOffset.UtcNow.AddMinutes(-5), "[]"),
            connection);
        await _verifications.UpsertAsync(
            new QuantityVerificationSnapshot(Guid.NewGuid().ToString("N"), project.Id, record.Id,
                VerificationSeverity.Pass, 1, DateTimeOffset.UtcNow, "[]"),
            connection);

        var results = await _verifications.GetByProjectAsync(project.Id, connection);

        var only = Assert.Single(results);
        Assert.Equal(VerificationSeverity.Pass, only.OverallSeverity);
    }

    private async Task<(Project Project, QuantityRecord Record)> InsertProjectAndRecordAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "검산 테스트 프로젝트", now, now, now);
        await _projects.InsertAsync(project, connection);

        var record = new QuantityRecord(Guid.NewGuid().ToString("N"), "Length", "A-WALL", 1, 10m, "m", "test.dwg", now)
        {
            ProjectId = project.Id,
        };
        await _quantityRecords.InsertAsync(record, connection);

        return (project, record);
    }
}
