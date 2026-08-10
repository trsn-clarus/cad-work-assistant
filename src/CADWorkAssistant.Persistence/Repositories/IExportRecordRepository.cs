using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IExportRecordRepository
{
    Task InsertAsync(ExportRecord record, SqliteConnection connection, SqliteTransaction? transaction = null);

    Task<IReadOnlyList<ExportRecord>> GetByProjectAsync(string projectId, SqliteConnection connection);

    /// <summary>Milestone 13 §143 - Output이 많은 프로젝트에서도 전체를 한 번에 불러오지 않는다.</summary>
    Task<IReadOnlyList<ExportRecord>> GetByProjectAsync(string projectId, int limit, SqliteConnection connection);
}
