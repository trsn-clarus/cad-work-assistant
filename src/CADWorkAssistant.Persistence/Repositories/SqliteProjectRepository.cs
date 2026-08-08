using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public sealed class SqliteProjectRepository : IProjectRepository
{
    public async Task InsertAsync(Project project, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Project (Id, Name, Client, Site, Description, CreatedAt, UpdatedAt, LastOpenedAt)
            VALUES ($id, $name, $client, $site, $description, $createdAt, $updatedAt, $lastOpenedAt);
            """;
        BindCommonParameters(command, project);
        command.Parameters.AddWithValue("$id", project.Id);
        command.Parameters.AddWithValue("$createdAt", SqliteValueConverters.ToDbText(project.CreatedAt));
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Project project, SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Project
            SET Name = $name, Client = $client, Site = $site, Description = $description,
                UpdatedAt = $updatedAt, LastOpenedAt = $lastOpenedAt
            WHERE Id = $id;
            """;
        BindCommonParameters(command, project);
        command.Parameters.AddWithValue("$id", project.Id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Project?> FindByIdAsync(string id, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Project WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadProject(reader) : null;
    }

    public async Task<IReadOnlyList<Project>> GetRecentAsync(int limit, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Project ORDER BY LastOpenedAt DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        return await ReadAllAsync(command);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Project ORDER BY Name COLLATE NOCASE;";
        return await ReadAllAsync(command);
    }

    public async Task TouchLastOpenedAsync(string id, DateTimeOffset openedAt, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Project SET LastOpenedAt = $lastOpenedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$lastOpenedAt", SqliteValueConverters.ToDbText(openedAt));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static void BindCommonParameters(SqliteCommand command, Project project)
    {
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$client", (object?)project.Client ?? DBNull.Value);
        command.Parameters.AddWithValue("$site", (object?)project.Site ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)project.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", SqliteValueConverters.ToDbText(project.UpdatedAt));
        command.Parameters.AddWithValue("$lastOpenedAt", SqliteValueConverters.ToDbText(project.LastOpenedAt));
    }

    private static async Task<IReadOnlyList<Project>> ReadAllAsync(SqliteCommand command)
    {
        var results = new List<Project>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadProject(reader));
        }

        return results;
    }

    private static Project ReadProject(SqliteDataReader reader)
    {
        return new Project(
            id: reader.GetString(reader.GetOrdinal("Id")),
            name: reader.GetString(reader.GetOrdinal("Name")),
            createdAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            updatedAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            lastOpenedAt: SqliteValueConverters.ParseDateTimeOffset(reader.GetString(reader.GetOrdinal("LastOpenedAt"))),
            client: reader.IsDBNull(reader.GetOrdinal("Client")) ? null : reader.GetString(reader.GetOrdinal("Client")),
            site: reader.IsDBNull(reader.GetOrdinal("Site")) ? null : reader.GetString(reader.GetOrdinal("Site")),
            description: reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")));
    }
}
