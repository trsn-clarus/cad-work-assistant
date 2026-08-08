using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteRecentMeasurementRepository : IRecentMeasurementRepository
{
    public async Task UpsertAsync(RecentMeasurement measurement, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RecentMeasurement (Id, ProjectId, MeasurementType, Value, Unit, SourceDrawing, ObjectHandlesJson, CreatedAt)
            VALUES ($id, $projectId, $measurementType, $value, $unit, $sourceDrawing, $objectHandlesJson, $createdAt)
            ON CONFLICT(ProjectId, MeasurementType) DO UPDATE SET
                Id = excluded.Id,
                Value = excluded.Value,
                Unit = excluded.Unit,
                SourceDrawing = excluded.SourceDrawing,
                ObjectHandlesJson = excluded.ObjectHandlesJson,
                CreatedAt = excluded.CreatedAt;
            """;
        command.Parameters.AddWithValue("$id", measurement.Id);
        command.Parameters.AddWithValue("$projectId", measurement.ProjectId);
        command.Parameters.AddWithValue("$measurementType", measurement.MeasurementType);
        command.Parameters.AddWithValue("$value", SqliteValueConverters.ToDbText(measurement.Value));
        command.Parameters.AddWithValue("$unit", measurement.Unit);
        command.Parameters.AddWithValue("$sourceDrawing", (object?)measurement.SourceDrawing ?? System.DBNull.Value);
        command.Parameters.AddWithValue("$objectHandlesJson", (object?)SqliteValueConverters.ToDbJson(measurement.ObjectHandles) ?? System.DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", SqliteValueConverters.ToDbText(measurement.CreatedAt));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<RecentMeasurement>> GetByProjectAsync(string projectId, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM RecentMeasurement WHERE ProjectId = $projectId;";
        command.Parameters.AddWithValue("$projectId", projectId);

        var results = new List<RecentMeasurement>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var sourceDrawingOrdinal = reader.GetOrdinal("SourceDrawing");
            var handlesOrdinal = reader.GetOrdinal("ObjectHandlesJson");
            results.Add(new RecentMeasurement(
                id: reader.GetString(reader.GetOrdinal("Id")),
                projectId: reader.GetString(reader.GetOrdinal("ProjectId")),
                measurementType: reader.GetString(reader.GetOrdinal("MeasurementType")),
                value: SqliteValueConverters.ParseDecimal(reader.GetString(reader.GetOrdinal("Value"))),
                unit: reader.GetString(reader.GetOrdinal("Unit")),
                sourceDrawing: reader.IsDBNull(sourceDrawingOrdinal) ? null : reader.GetString(sourceDrawingOrdinal),
                objectHandles: SqliteValueConverters.ParseStringList(reader.IsDBNull(handlesOrdinal) ? null : reader.GetString(handlesOrdinal)),
                createdAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("CreatedAt")))));
        }

        return results;
    }
}
