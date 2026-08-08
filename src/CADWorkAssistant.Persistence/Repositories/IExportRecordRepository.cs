using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IExportRecordRepository
{
    Task InsertAsync(ExportRecord record, SqliteConnection connection, SqliteTransaction? transaction = null);

    Task<IReadOnlyList<ExportRecord>> GetByProjectAsync(string projectId, SqliteConnection connection);
}
