using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteQuantityVerificationRepository : IQuantityVerificationRepository
{
    public async Task UpsertAsync(QuantityVerificationSnapshot snapshot, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO QuantityVerificationSnapshot (Id, ProjectId, QuantityRecordId, OverallSeverity, RuleSetVersion, CheckedAt, ChecksJson)
            VALUES ($id, $projectId, $quantityRecordId, $overallSeverity, $ruleSetVersion, $checkedAt, $checksJson)
            ON CONFLICT(QuantityRecordId) DO UPDATE SET
                Id = excluded.Id,
                OverallSeverity = excluded.OverallSeverity,
                RuleSetVersion = excluded.RuleSetVersion,
                CheckedAt = excluded.CheckedAt,
                ChecksJson = excluded.ChecksJson;
            """;
        command.Parameters.AddWithValue("$id", snapshot.Id);
        command.Parameters.AddWithValue("$projectId", snapshot.ProjectId);
        command.Parameters.AddWithValue("$quantityRecordId", snapshot.QuantityRecordId);
        command.Parameters.AddWithValue("$overallSeverity", snapshot.OverallSeverity.ToString());
        command.Parameters.AddWithValue("$ruleSetVersion", snapshot.RuleSetVersion);
        command.Parameters.AddWithValue("$checkedAt", SqliteValueConverters.ToDbText(snapshot.CheckedAt));
        command.Parameters.AddWithValue("$checksJson", snapshot.ChecksJson);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<QuantityVerificationSnapshot>> GetByProjectAsync(string projectId, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM QuantityVerificationSnapshot WHERE ProjectId = $projectId;";
        command.Parameters.AddWithValue("$projectId", projectId);

        var results = new List<QuantityVerificationSnapshot>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new QuantityVerificationSnapshot(
                id: reader.GetString(reader.GetOrdinal("Id")),
                projectId: reader.GetString(reader.GetOrdinal("ProjectId")),
                quantityRecordId: reader.GetString(reader.GetOrdinal("QuantityRecordId")),
                overallSeverity: Enum.Parse<VerificationSeverity>(reader.GetString(reader.GetOrdinal("OverallSeverity"))),
                ruleSetVersion: reader.GetInt32(reader.GetOrdinal("RuleSetVersion")),
                checkedAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("CheckedAt"))),
                checksJson: reader.GetString(reader.GetOrdinal("ChecksJson"))));
        }

        return results;
    }
}
