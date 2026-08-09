using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Migrations;

/// <summary>
/// v3 - Milestone 9 §80-81. 기존 ExportRecord 행(전부 Milestone 5의 DWG WBLOCK)은 DEFAULT로
/// 'DwgSelection'을 받아 그대로 유효하게 남는다 - 데이터 손실/재작성 없는 단순 컬럼 추가다.
/// </summary>
internal sealed class Migration003AddExportType : IMigration
{
    public int Version => 3;

    public string Description => "Add ExportType column to ExportRecord";

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE ExportRecord ADD COLUMN ExportType TEXT NOT NULL DEFAULT 'DwgSelection';
            """;
        command.ExecuteNonQuery();
    }
}
