using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Repositories;

public interface IQuantityRecordRepository
{
    Task InsertAsync(QuantityRecord record, SqliteConnection connection, SqliteTransaction? transaction = null);

    /// <summary>Description만 사용자가 고칠 수 있다 (§57-58) - 계산값 자체(Value/ObjectCount 등)는
    /// 이번 범위에서 수정 대상이 아니다.</summary>
    Task UpdateDescriptionAsync(string id, string description, DateTimeOffset updatedAt, SqliteConnection connection);

    Task DeleteAsync(string id, SqliteConnection connection);

    Task<IReadOnlyList<QuantityRecord>> GetByProjectAsync(string projectId, SqliteConnection connection);
}
