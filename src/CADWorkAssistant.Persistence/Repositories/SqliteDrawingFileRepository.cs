using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteDrawingFileRepository : IDrawingFileRepository
{
    public async Task UpsertAsync(DrawingFile drawingFile, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DrawingFile (Id, ProjectId, FileName, FullPath, DrawingUnit, LastSeenAt, LastOpenedAt, IsMissing)
            VALUES ($id, $projectId, $fileName, $fullPath, $drawingUnit, $lastSeenAt, $lastOpenedAt, $isMissing)
            ON CONFLICT(ProjectId, FullPath) DO UPDATE SET
                FileName = excluded.FileName,
                DrawingUnit = excluded.DrawingUnit,
                LastSeenAt = excluded.LastSeenAt,
                LastOpenedAt = excluded.LastOpenedAt,
                IsMissing = excluded.IsMissing;
            """;
        command.Parameters.AddWithValue("$id", drawingFile.Id);
        command.Parameters.AddWithValue("$projectId", drawingFile.ProjectId);
        command.Parameters.AddWithValue("$fileName", drawingFile.FileName);
        command.Parameters.AddWithValue("$fullPath", drawingFile.FullPath);
        command.Parameters.AddWithValue("$drawingUnit", (object?)drawingFile.DrawingUnit ?? System.DBNull.Value);
        command.Parameters.AddWithValue("$lastSeenAt", SqliteValueConverters.ToDbText(drawingFile.LastSeenAt));
        command.Parameters.AddWithValue("$lastOpenedAt", SqliteValueConverters.ToDbText(drawingFile.LastOpenedAt));
        command.Parameters.AddWithValue("$isMissing", drawingFile.IsMissing ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<DrawingFile>> GetByProjectAsync(string projectId, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DrawingFile WHERE ProjectId = $projectId ORDER BY LastOpenedAt DESC;";
        command.Parameters.AddWithValue("$projectId", projectId);

        var results = new List<DrawingFile>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var drawingUnitOrdinal = reader.GetOrdinal("DrawingUnit");
            results.Add(new DrawingFile(
                id: reader.GetString(reader.GetOrdinal("Id")),
                projectId: reader.GetString(reader.GetOrdinal("ProjectId")),
                fileName: reader.GetString(reader.GetOrdinal("FileName")),
                fullPath: reader.GetString(reader.GetOrdinal("FullPath")),
                drawingUnit: reader.IsDBNull(drawingUnitOrdinal) ? null : reader.GetString(drawingUnitOrdinal),
                lastSeenAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("LastSeenAt"))),
                lastOpenedAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("LastOpenedAt"))),
                isMissing: reader.GetInt32(reader.GetOrdinal("IsMissing")) != 0));
        }

        return results;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetCountsByProjectAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProjectId, COUNT(*) AS Cnt FROM DrawingFile GROUP BY ProjectId;";

        var results = new Dictionary<string, int>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results[reader.GetString(0)] = reader.GetInt32(1);
        }

        return results;
    }

    public async Task RelinkAsync(string id, string newFullPath, string newFileName, string? newDrawingUnit, DateTimeOffset relinkedAt, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE DrawingFile
            SET FullPath = $fullPath, FileName = $fileName, DrawingUnit = $drawingUnit,
                LastSeenAt = $lastSeenAt, IsMissing = 0
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$fullPath", newFullPath);
        command.Parameters.AddWithValue("$fileName", newFileName);
        command.Parameters.AddWithValue("$drawingUnit", (object?)newDrawingUnit ?? System.DBNull.Value);
        command.Parameters.AddWithValue("$lastSeenAt", SqliteValueConverters.ToDbText(relinkedAt));
        await command.ExecuteNonQueryAsync();
    }
}
