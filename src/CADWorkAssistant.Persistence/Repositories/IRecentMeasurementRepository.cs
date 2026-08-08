using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IRecentMeasurementRepository
{
    /// <summary>(ProjectId, MeasurementType) 조합마다 마지막 값 하나만 의미가 있다 - 이미 있으면
    /// 덮어쓴다 (upsert-latest-only, UNIQUE 인덱스 기반).</summary>
    Task UpsertAsync(RecentMeasurement measurement, SqliteConnection connection);

    Task<IReadOnlyList<RecentMeasurement>> GetByProjectAsync(string projectId, SqliteConnection connection);
}
