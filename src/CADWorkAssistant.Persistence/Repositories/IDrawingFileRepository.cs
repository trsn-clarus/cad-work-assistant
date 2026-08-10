using System;
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

    /// <summary>Milestone 13 §7 - Projects 목록의 "도면" 열을 위한 집계. 프로젝트마다 개별 조회하면
    /// N+1이 되므로 전체를 한 번에 GROUP BY로 가져온다(§145 성능).</summary>
    Task<IReadOnlyDictionary<string, int>> GetCountsByProjectAsync(SqliteConnection connection);

    /// <summary>Milestone 13 §23-25 - 기존 행의 FullPath 자체를 바꾼다. UpsertAsync의 ON CONFLICT는
    /// (ProjectId, FullPath)가 그대로일 때만 갱신을 처리하므로, 경로 자체가 바뀌는 "다시 연결"은
    /// Id로 직접 찾아 업데이트해야 한다. 과거 QuantityRecord의 SourceDrawing snapshot은 건드리지
    /// 않는다(§25) - 이 메서드는 DrawingFile 행 하나만 갱신한다.</summary>
    Task RelinkAsync(string id, string newFullPath, string newFileName, string? newDrawingUnit, DateTimeOffset relinkedAt, SqliteConnection connection, SqliteTransaction? transaction = null);
}
