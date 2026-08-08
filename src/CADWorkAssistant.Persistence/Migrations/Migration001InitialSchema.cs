using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Migrations;

/// <summary>
/// v1 - Project/QuantityRecord/ActivityRecord/DrawingFile/ExportRecord/RecentMeasurement 6개 표
/// (Milestone 6 §133 Data Model Diagram). decimal/DateTimeOffset/문자열배열은 전부
/// <see cref="SqliteValueConverters"/> 규칙대로 TEXT/TEXT(JSON)로 저장한다 - SQLite에 그 타입들이
/// 없어서다.
/// </summary>
internal sealed class Migration001InitialSchema : IMigration
{
    public int Version => 1;

    public string Description => "Initial schema: Project, QuantityRecord, ActivityRecord, DrawingFile, ExportRecord, RecentMeasurement";

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE Project (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Client TEXT NULL,
                Site TEXT NULL,
                Description TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastOpenedAt TEXT NOT NULL
            );
            CREATE INDEX IX_Project_LastOpenedAt ON Project(LastOpenedAt DESC);

            CREATE TABLE QuantityRecord (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Project(Id) ON DELETE CASCADE,
                Type TEXT NOT NULL,
                Layer TEXT NOT NULL,
                ObjectCount INTEGER NOT NULL,
                Value TEXT NOT NULL,
                Unit TEXT NOT NULL,
                SourceDrawing TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL,
                RawValue TEXT NULL,
                SourceUnit TEXT NULL,
                ObjectHandlesJson TEXT NULL,
                CalculationExpression TEXT NULL,
                MeasurementSource TEXT NULL,
                CalculationMetadataJson TEXT NULL,
                Description TEXT NOT NULL DEFAULT '',
                SortOrder INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IX_QuantityRecord_ProjectId ON QuantityRecord(ProjectId);

            CREATE TABLE ActivityRecord (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Project(Id) ON DELETE CASCADE,
                ActivityType TEXT NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NULL,
                CreatedAt TEXT NOT NULL,
                MetadataJson TEXT NULL
            );
            CREATE INDEX IX_ActivityRecord_ProjectId_CreatedAt ON ActivityRecord(ProjectId, CreatedAt DESC);

            CREATE TABLE DrawingFile (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Project(Id) ON DELETE CASCADE,
                FileName TEXT NOT NULL,
                FullPath TEXT NOT NULL,
                DrawingUnit TEXT NULL,
                LastSeenAt TEXT NOT NULL,
                LastOpenedAt TEXT NOT NULL,
                IsMissing INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IX_DrawingFile_ProjectId ON DrawingFile(ProjectId);
            CREATE UNIQUE INDEX UX_DrawingFile_ProjectId_FullPath ON DrawingFile(ProjectId, FullPath);

            CREATE TABLE ExportRecord (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Project(Id) ON DELETE CASCADE,
                SourceDrawing TEXT NULL,
                TargetFile TEXT NOT NULL,
                ObjectCount INTEGER NOT NULL,
                Description TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_ExportRecord_ProjectId ON ExportRecord(ProjectId);

            CREATE TABLE RecentMeasurement (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Project(Id) ON DELETE CASCADE,
                MeasurementType TEXT NOT NULL,
                Value TEXT NOT NULL,
                Unit TEXT NOT NULL,
                SourceDrawing TEXT NULL,
                ObjectHandlesJson TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX UX_RecentMeasurement_ProjectId_Type ON RecentMeasurement(ProjectId, MeasurementType);
            """;
        command.ExecuteNonQuery();
    }
}
