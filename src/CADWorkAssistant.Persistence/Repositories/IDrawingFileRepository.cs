using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IDrawingFileRepository
{
    /// <summary>같은 (ProjectId, FullPath)면 새 행을 만들지 않고 FileName/DrawingUnit/LastSeenAt/
    /// LastOpenedAt/IsMissing만 갱신한다 (§97-98 중복 방지, UNIQUE 인덱스 기반).</summary>
    Task UpsertAsync(DrawingFile drawingFile, SqliteConnection connection);

    Task<IReadOnlyList<DrawingFile>> GetByProjectAsync(string projectId, SqliteConnection connection);
}
