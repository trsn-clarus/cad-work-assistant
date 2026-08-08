using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IProjectRepository
{
    Task InsertAsync(Project project, SqliteConnection connection, SqliteTransaction? transaction = null);

    /// <summary>Name/Client/Site/Description/UpdatedAt/LastOpenedAt를 갱신한다. Id/CreatedAt은 불변.</summary>
    Task UpdateAsync(Project project, SqliteConnection connection, SqliteTransaction? transaction = null);

    Task<Project?> FindByIdAsync(string id, SqliteConnection connection);

    /// <summary>LastOpenedAt DESC 정렬 - "최근 프로젝트" 목록/전환 UI용 (§34-36).</summary>
    Task<IReadOnlyList<Project>> GetRecentAsync(int limit, SqliteConnection connection);

    Task<IReadOnlyList<Project>> GetAllAsync(SqliteConnection connection);

    /// <summary>프로젝트를 열 때 LastOpenedAt만 갱신한다 (§35).</summary>
    Task TouchLastOpenedAsync(string id, System.DateTimeOffset openedAt, SqliteConnection connection);
}
