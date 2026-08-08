using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Tests;

public sealed class DatabaseMigrationTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public DatabaseMigrationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void OpenConnection_FirstTime_CreatesAllEightTablesAndSetsUserVersion()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA user_version;";
            var version = (long)pragma.ExecuteScalar()!;
            // Migration001(v1, Milestone 6) + Migration002(v2, Milestone 7 Verification/Review) 둘 다 적용.
            Assert.Equal(2, version);
        }

        var expectedTables = new[]
        {
            "Project", "QuantityRecord", "ActivityRecord", "DrawingFile", "ExportRecord", "RecentMeasurement",
            "QuantityVerificationSnapshot", "QuantityReview",
        };
        foreach (var table in expectedTables)
        {
            Assert.True(TableExists(connection, table), $"Table {table} should exist after migration.");
        }
    }

    [Fact]
    public void OpenConnection_SecondTime_DoesNotReapplyMigration()
    {
        var database = _fixture.CreateDatabase();
        using (var connection = database.OpenConnection())
        {
            InsertMinimalProject(connection, "p1");
        }

        // 재오픈해도(마이그레이션 재적용 시도) 기존 데이터가 그대로 남아 있어야 한다 - 파괴적
        // "지우고 새로 만들기"가 없다는 것의 직접적인 증거.
        using var reopened = database.OpenConnection();
        using var command = reopened.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Project WHERE Id = 'p1';";
        var count = (long)command.ExecuteScalar()!;
        Assert.Equal(1, count);
    }

    [Fact]
    public void OpenConnection_EnablesForeignKeyEnforcement()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        var enabled = (long)command.ExecuteScalar()!;
        Assert.Equal(1, enabled);
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return (long)command.ExecuteScalar()! == 1;
    }

    private static void InsertMinimalProject(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Project (Id, Name, CreatedAt, UpdatedAt, LastOpenedAt)
            VALUES ($id, 'test', '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z');
            """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
}
