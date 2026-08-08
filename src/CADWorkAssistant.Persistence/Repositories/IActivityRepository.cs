using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IActivityRepository
{
    Task InsertAsync(ActivityRecord activity, SqliteConnection connection, SqliteTransaction? transaction = null);

    /// <summary>CreatedAt DESC(최신 먼저), limit으로 화면 표시 개수를 제한한다 (§39-40 - 진단 로그가
    /// 아니라 사용자용 업무 히스토리이므로 무한정 쌓인 걸 다 보여줄 필요가 없다).</summary>
    Task<IReadOnlyList<ActivityRecord>> GetByProjectAsync(string projectId, int limit, SqliteConnection connection);
}
