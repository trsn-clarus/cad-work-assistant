using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteQuantityRecordRepository : IQuantityRecordRepository
{
    public async Task InsertAsync(QuantityRecord record, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO QuantityRecord (
                Id, ProjectId, Type, Layer, ObjectCount, Value, Unit, SourceDrawing, CreatedAt,
                UpdatedAt, RawValue, SourceUnit, ObjectHandlesJson, CalculationExpression,
                MeasurementSource, CalculationMetadataJson, Description, SortOrder)
            VALUES (
                $id, $projectId, $type, $layer, $objectCount, $value, $unit, $sourceDrawing, $createdAt,
                $updatedAt, $rawValue, $sourceUnit, $objectHandlesJson, $calculationExpression,
                $measurementSource, $calculationMetadataJson, $description, $sortOrder);
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$projectId", record.ProjectId);
        command.Parameters.AddWithValue("$type", record.Type);
        command.Parameters.AddWithValue("$layer", record.Layer);
        command.Parameters.AddWithValue("$objectCount", record.ObjectCount);
        command.Parameters.AddWithValue("$value", SqliteValueConverters.ToDbText(record.Value));
        command.Parameters.AddWithValue("$unit", record.Unit);
        command.Parameters.AddWithValue("$sourceDrawing", record.SourceDrawing);
        command.Parameters.AddWithValue("$createdAt", SqliteValueConverters.ToDbText(record.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", record.UpdatedAt is null ? DBNull.Value : SqliteValueConverters.ToDbText(record.UpdatedAt.Value));
        command.Parameters.AddWithValue("$rawValue", record.RawValue is null ? DBNull.Value : SqliteValueConverters.ToDbText(record.RawValue.Value));
        command.Parameters.AddWithValue("$sourceUnit", (object?)record.SourceUnit ?? DBNull.Value);
        command.Parameters.AddWithValue("$objectHandlesJson", (object?)SqliteValueConverters.ToDbJson(record.ObjectHandles) ?? DBNull.Value);
        command.Parameters.AddWithValue("$calculationExpression", (object?)record.CalculationExpression ?? DBNull.Value);
        command.Parameters.AddWithValue("$measurementSource", (object?)record.MeasurementSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$calculationMetadataJson", (object?)record.CalculationMetadataJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", record.Description);
        command.Parameters.AddWithValue("$sortOrder", 0);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateDescriptionAsync(string id, string description, DateTimeOffset updatedAt, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE QuantityRecord SET Description = $description, UpdatedAt = $updatedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$updatedAt", SqliteValueConverters.ToDbText(updatedAt));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string id, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM QuantityRecord WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<QuantityRecord>> GetByProjectAsync(string projectId, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM QuantityRecord WHERE ProjectId = $projectId ORDER BY CreatedAt;";
        command.Parameters.AddWithValue("$projectId", projectId);

        var results = new List<QuantityRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var updatedAtOrdinal = reader.GetOrdinal("UpdatedAt");
            var rawValueOrdinal = reader.GetOrdinal("RawValue");
            var sourceUnitOrdinal = reader.GetOrdinal("SourceUnit");
            var handlesOrdinal = reader.GetOrdinal("ObjectHandlesJson");
            var expressionOrdinal = reader.GetOrdinal("CalculationExpression");
            var measurementSourceOrdinal = reader.GetOrdinal("MeasurementSource");
            var metadataOrdinal = reader.GetOrdinal("CalculationMetadataJson");

            var record = new QuantityRecord(
                id: reader.GetString(reader.GetOrdinal("Id")),
                type: reader.GetString(reader.GetOrdinal("Type")),
                layer: reader.GetString(reader.GetOrdinal("Layer")),
                objectCount: reader.GetInt32(reader.GetOrdinal("ObjectCount")),
                value: SqliteValueConverters.ParseDecimal(reader.GetString(reader.GetOrdinal("Value"))),
                unit: reader.GetString(reader.GetOrdinal("Unit")),
                sourceDrawing: reader.GetString(reader.GetOrdinal("SourceDrawing")),
                createdAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                rawValue: reader.IsDBNull(rawValueOrdinal) ? null : SqliteValueConverters.ParseDecimal(reader.GetString(rawValueOrdinal)),
                sourceUnit: reader.IsDBNull(sourceUnitOrdinal) ? null : reader.GetString(sourceUnitOrdinal),
                objectHandles: SqliteValueConverters.ParseStringList(reader.IsDBNull(handlesOrdinal) ? null : reader.GetString(handlesOrdinal)),
                calculationExpression: reader.IsDBNull(expressionOrdinal) ? null : reader.GetString(expressionOrdinal),
                measurementSource: reader.IsDBNull(measurementSourceOrdinal) ? null : reader.GetString(measurementSourceOrdinal),
                calculationMetadataJson: reader.IsDBNull(metadataOrdinal) ? null : reader.GetString(metadataOrdinal))
            {
                ProjectId = reader.GetString(reader.GetOrdinal("ProjectId")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                UpdatedAt = reader.IsDBNull(updatedAtOrdinal) ? null : SqliteValueConverters.ParseDateTimeOffset(reader.GetString(updatedAtOrdinal)),
            };
            results.Add(record);
        }

        return results;
    }
}
