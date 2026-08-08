using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteActivityRepository : IActivityRepository
{
    public async Task InsertAsync(ActivityRecord activity, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ActivityRecord (Id, ProjectId, ActivityType, Title, Description, CreatedAt, MetadataJson)
            VALUES ($id, $projectId, $activityType, $title, $description, $createdAt, $metadataJson);
            """;
        command.Parameters.AddWithValue("$id", activity.Id);
        command.Parameters.AddWithValue("$projectId", activity.ProjectId);
        command.Parameters.AddWithValue("$activityType", activity.ActivityType);
        command.Parameters.AddWithValue("$title", activity.Title);
        command.Parameters.AddWithValue("$description", (object?)activity.Description ?? System.DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", SqliteValueConverters.ToDbText(activity.CreatedAt));
        command.Parameters.AddWithValue("$metadataJson", (object?)activity.MetadataJson ?? System.DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ActivityRecord>> GetByProjectAsync(string projectId, int limit, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ActivityRecord WHERE ProjectId = $projectId ORDER BY CreatedAt DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<ActivityRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var descriptionOrdinal = reader.GetOrdinal("Description");
            var metadataOrdinal = reader.GetOrdinal("MetadataJson");
            results.Add(new ActivityRecord(
                id: reader.GetString(reader.GetOrdinal("Id")),
                projectId: reader.GetString(reader.GetOrdinal("ProjectId")),
                activityType: reader.GetString(reader.GetOrdinal("ActivityType")),
                title: reader.GetString(reader.GetOrdinal("Title")),
                description: reader.IsDBNull(descriptionOrdinal) ? null : reader.GetString(descriptionOrdinal),
                createdAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                metadataJson: reader.IsDBNull(metadataOrdinal) ? null : reader.GetString(metadataOrdinal)));
        }

        return results;
    }
}
