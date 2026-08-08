using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteQuantityReviewRepository : IQuantityReviewRepository
{
    public async Task UpsertAsync(QuantityReview review, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO QuantityReview (Id, ProjectId, QuantityRecordId, Status, Note, ReviewedAt)
            VALUES ($id, $projectId, $quantityRecordId, $status, $note, $reviewedAt)
            ON CONFLICT(QuantityRecordId) DO UPDATE SET
                Id = excluded.Id,
                Status = excluded.Status,
                Note = excluded.Note,
                ReviewedAt = excluded.ReviewedAt;
            """;
        command.Parameters.AddWithValue("$id", review.Id);
        command.Parameters.AddWithValue("$projectId", review.ProjectId);
        command.Parameters.AddWithValue("$quantityRecordId", review.QuantityRecordId);
        command.Parameters.AddWithValue("$status", review.Status.ToString());
        command.Parameters.AddWithValue("$note", (object?)review.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$reviewedAt", review.ReviewedAt is null ? DBNull.Value : SqliteValueConverters.ToDbText(review.ReviewedAt.Value));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<QuantityReview>> GetByProjectAsync(string projectId, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM QuantityReview WHERE ProjectId = $projectId;";
        command.Parameters.AddWithValue("$projectId", projectId);

        var results = new List<QuantityReview>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var noteOrdinal = reader.GetOrdinal("Note");
            var reviewedAtOrdinal = reader.GetOrdinal("ReviewedAt");
            results.Add(new QuantityReview(
                id: reader.GetString(reader.GetOrdinal("Id")),
                projectId: reader.GetString(reader.GetOrdinal("ProjectId")),
                quantityRecordId: reader.GetString(reader.GetOrdinal("QuantityRecordId")),
                status: Enum.Parse<QuantityReviewStatus>(reader.GetString(reader.GetOrdinal("Status"))),
                note: reader.IsDBNull(noteOrdinal) ? null : reader.GetString(noteOrdinal),
                reviewedAt: reader.IsDBNull(reviewedAtOrdinal) ? null : SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reviewedAtOrdinal))));
        }

        return results;
    }
}
