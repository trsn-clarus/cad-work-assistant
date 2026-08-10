using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteExportRecordRepository : IExportRecordRepository
{
    public async Task InsertAsync(ExportRecord record, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ExportRecord (Id, ProjectId, SourceDrawing, TargetFile, ObjectCount, Description, CreatedAt, ExportType)
            VALUES ($id, $projectId, $sourceDrawing, $targetFile, $objectCount, $description, $createdAt, $exportType);
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$projectId", record.ProjectId);
        command.Parameters.AddWithValue("$sourceDrawing", (object?)record.SourceDrawing ?? System.DBNull.Value);
        command.Parameters.AddWithValue("$targetFile", record.TargetFile);
        command.Parameters.AddWithValue("$objectCount", record.ObjectCount);
        command.Parameters.AddWithValue("$description", (object?)record.Description ?? System.DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", SqliteValueConverters.ToDbText(record.CreatedAt));
        command.Parameters.AddWithValue("$exportType", record.ExportType);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ExportRecord>> GetByProjectAsync(string projectId, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ExportRecord WHERE ProjectId = $projectId ORDER BY CreatedAt DESC;";
        command.Parameters.AddWithValue("$projectId", projectId);
        return await ReadAllAsync(command);
    }

    public async Task<IReadOnlyList<ExportRecord>> GetByProjectAsync(string projectId, int limit, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ExportRecord WHERE ProjectId = $projectId ORDER BY CreatedAt DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$limit", limit);
        return await ReadAllAsync(command);
    }

    private static async Task<IReadOnlyList<ExportRecord>> ReadAllAsync(SqliteCommand command)
    {
        var results = new List<ExportRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var sourceDrawingOrdinal = reader.GetOrdinal("SourceDrawing");
            var descriptionOrdinal = reader.GetOrdinal("Description");
            results.Add(new ExportRecord(
                id: reader.GetString(reader.GetOrdinal("Id")),
                projectId: reader.GetString(reader.GetOrdinal("ProjectId")),
                sourceDrawing: reader.IsDBNull(sourceDrawingOrdinal) ? null : reader.GetString(sourceDrawingOrdinal),
                targetFile: reader.GetString(reader.GetOrdinal("TargetFile")),
                objectCount: reader.GetInt32(reader.GetOrdinal("ObjectCount")),
                description: reader.IsDBNull(descriptionOrdinal) ? null : reader.GetString(descriptionOrdinal),
                createdAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                exportType: reader.GetString(reader.GetOrdinal("ExportType"))));
        }

        return results;
    }
}
