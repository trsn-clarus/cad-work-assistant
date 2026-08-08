using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IQuantityVerificationRepository
{
    /// <summary>QuantityRecordId당 최신 Snapshot 하나만 남는다 - 재검산하면 이전 결과를 덮어쓴다
    /// (upsert-latest-only, UNIQUE 인덱스 기반).</summary>
    Task UpsertAsync(QuantityVerificationSnapshot snapshot, SqliteConnection connection, SqliteTransaction? transaction = null);

    /// <summary>History 화면이 한 번에 전부 불러와 메모리에서 QuantityRecordId로 찾아 쓴다 - 레코드마다
    /// 개별 쿼리하는 N+1을 피한다(§94).</summary>
    Task<IReadOnlyList<QuantityVerificationSnapshot>> GetByProjectAsync(string projectId, SqliteConnection connection);
}
