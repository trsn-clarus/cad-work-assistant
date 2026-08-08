using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Migrations;

/// <summary>
/// v2 - Milestone 7 Verification/Review. 기존 QuantityRecord 테이블은 건드리지 않는다 -
/// CalculationMetadataJson 컬럼은 이미 v1에 있었고(당시엔 아무도 채우지 않았을 뿐이다), 이번에
/// 실제로 쓰기 시작한다. 두 테이블 모두 QuantityRecord당 "최신 상태 하나"만 의미가 있어
/// (upsert-latest-only) UNIQUE(QuantityRecordId) 인덱스를 둔다 - RecentMeasurement(v1)와 같은 패턴.
/// </summary>
internal sealed class Migration002AddVerificationAndReview : IMigration
{
    public int Version => 2;

    public string Description => "Add QuantityVerificationSnapshot and QuantityReview tables";

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE QuantityVerificationSnapshot (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Project(Id) ON DELETE CASCADE,
                QuantityRecordId TEXT NOT NULL REFERENCES QuantityRecord(Id) ON DELETE CASCADE,
                OverallSeverity TEXT NOT NULL,
                RuleSetVersion INTEGER NOT NULL,
                CheckedAt TEXT NOT NULL,
                ChecksJson TEXT NOT NULL
            );
            CREATE INDEX IX_QuantityVerificationSnapshot_ProjectId ON QuantityVerificationSnapshot(ProjectId);
            CREATE UNIQUE INDEX UX_QuantityVerificationSnapshot_QuantityRecordId ON QuantityVerificationSnapshot(QuantityRecordId);

            CREATE TABLE QuantityReview (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Project(Id) ON DELETE CASCADE,
                QuantityRecordId TEXT NOT NULL REFERENCES QuantityRecord(Id) ON DELETE CASCADE,
                Status TEXT NOT NULL DEFAULT 'Unreviewed',
                Note TEXT NULL,
                ReviewedAt TEXT NULL
            );
            CREATE INDEX IX_QuantityReview_ProjectId ON QuantityReview(ProjectId);
            CREATE UNIQUE INDEX UX_QuantityReview_QuantityRecordId ON QuantityReview(QuantityRecordId);
            """;
        command.ExecuteNonQuery();
    }
}
