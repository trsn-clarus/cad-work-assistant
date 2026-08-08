using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class QuantityRecordRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _projects = new SqliteProjectRepository();
    private readonly IQuantityRecordRepository _records = new SqliteQuantityRecordRepository();

    public QuantityRecordRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InsertAsync_DecimalValue_RoundTripsExactly()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);

        // 255.941 - QuantityRecord.Value의 decimal 정밀도가 REAL(부동소수점) 저장에서 깨진 적이
        // 있었던 값이라 회귀 방지 차원에서 그대로 쓴다 (SqliteValueConverters 주석 참고).
        var record = new QuantityRecord(
            id: Guid.NewGuid().ToString("N"),
            type: "VerticalArea",
            layer: "A-WALL",
            objectCount: 3,
            value: 255.941m,
            unit: "m²",
            sourceDrawing: "test.dwg",
            createdAt: DateTimeOffset.UtcNow,
            rawValue: 255941m,
            sourceUnit: "mm",
            objectHandles: new[] { "1A2B", "1A2C" },
            calculationExpression: "125.331 + 81.405 + 49.205 = 255.941 m",
            measurementSource: "CadSelection",
            calculationMetadataJson: "{\"height\":3000,\"heightUnit\":\"mm\"}")
        {
            ProjectId = project.Id,
        };

        await _records.InsertAsync(record, connection);
        var found = (await _records.GetByProjectAsync(project.Id, connection)).Single();

        Assert.Equal(255.941m, found.Value);
        Assert.Equal(255941m, found.RawValue);
        Assert.Equal(new[] { "1A2B", "1A2C" }, found.ObjectHandles);
        Assert.Equal(record.CalculationExpression, found.CalculationExpression);
        Assert.Equal(record.CalculationMetadataJson, found.CalculationMetadataJson);
        Assert.Equal(record.MeasurementSource, found.MeasurementSource);
    }

    [Fact]
    public async Task UpdateDescriptionAsync_ChangesDescriptionAndUpdatedAt_ButNotValue()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);
        var record = CreateMinimalRecord(project.Id, 10.5m);
        await _records.InsertAsync(record, connection);

        var updatedAt = DateTimeOffset.UtcNow;
        await _records.UpdateDescriptionAsync(record.Id, "현장 메모 추가", updatedAt, connection);

        var found = (await _records.GetByProjectAsync(project.Id, connection)).Single();
        Assert.Equal("현장 메모 추가", found.Description);
        Assert.Equal(10.5m, found.Value);
        Assert.NotNull(found.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecord()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);
        var record = CreateMinimalRecord(project.Id, 1m);
        await _records.InsertAsync(record, connection);

        await _records.DeleteAsync(record.Id, connection);

        var remaining = await _records.GetByProjectAsync(project.Id, connection);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task DeletingProject_CascadesDeleteOfItsQuantityRecords()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        var project = await InsertProjectAsync(connection);
        await _records.InsertAsync(CreateMinimalRecord(project.Id, 1m), connection);
        await _records.InsertAsync(CreateMinimalRecord(project.Id, 2m), connection);

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = "DELETE FROM Project WHERE Id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", project.Id);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        var remaining = await _records.GetByProjectAsync(project.Id, connection);
        Assert.Empty(remaining);
    }

    private async Task<Project> InsertProjectAsync(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid().ToString("N"), "테스트 프로젝트", now, now, now);
        await _projects.InsertAsync(project, connection);
        return project;
    }

    private static QuantityRecord CreateMinimalRecord(string projectId, decimal value) =>
        new(
            id: Guid.NewGuid().ToString("N"),
            type: "Length",
            layer: "A-DOOR",
            objectCount: 1,
            value: value,
            unit: "m",
            sourceDrawing: "test.dwg",
            createdAt: DateTimeOffset.UtcNow)
        {
            ProjectId = projectId,
        };
}
