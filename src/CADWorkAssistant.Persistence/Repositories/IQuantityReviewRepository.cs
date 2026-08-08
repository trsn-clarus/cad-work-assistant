using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IQuantityReviewRepository
{
    /// <summary>QuantityRecordId당 최신 검토 상태 하나만 남는다 (upsert-latest-only).</summary>
    Task UpsertAsync(QuantityReview review, SqliteConnection connection, SqliteTransaction? transaction = null);

    Task<IReadOnlyList<QuantityReview>> GetByProjectAsync(string projectId, SqliteConnection connection);
}
