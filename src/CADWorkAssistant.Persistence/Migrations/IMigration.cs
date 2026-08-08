using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Migrations;

/// <summary>
/// 스키마 변경 한 단계 (Milestone 6 §10, §12) - "v1 Initial Schema, v2 Add X, v3 Add Y"처럼 새
/// 마이그레이션은 새 클래스를 추가하고 <see cref="DatabaseMigrator"/>의 목록에 등록한다. 기존
/// 마이그레이션은 절대 수정하지 않는다 - 이미 적용된 사용자 DB와 어긋난다.
/// </summary>
internal interface IMigration
{
    /// <summary>1부터 시작하는 순번. <c>PRAGMA user_version</c>과 직접 대응한다.</summary>
    int Version { get; }

    string Description { get; }

    void Apply(SqliteConnection connection, SqliteTransaction transaction);
}
