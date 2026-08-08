using System;
using System.IO;
using Microsoft.Data.Sqlite;
using CADWorkAssistant.Persistence.Migrations;

namespace CADWorkAssistant.Persistence;

/// <summary>
/// DB 파일 경로 결정 + 연결 생성의 단일 진입점 (Milestone 6 §8-9). 연결은 pooled/long-lived로
/// 들고 있지 않는다 - SQLite 파일 연결은 열고 닫는 비용이 낮고, WAL 모드+짧은 사용 패턴이면
/// UI/백그라운드 스레드 간 하나의 연결을 공유하는 복잡도를 피할 수 있다. 호출부는
/// <c>using var connection = database.OpenConnection();</c> 형태로 매 작업마다 새로 연다.
/// </summary>
public sealed class CadWorkAssistantDatabase
{
    private const string DatabasePathEnvVar = "CWA_DATABASE_PATH";
    private const string SimulationModeEnvVar = "CWA_USE_FAKE_AUTOCAD";

    private readonly string _connectionString;

    public CadWorkAssistantDatabase(string? databasePathOverride = null)
    {
        DatabasePath = databasePathOverride ?? ResolveDefaultDatabasePath();
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    /// <summary>
    /// 새 연결을 열고 PRAGMA(WAL/foreign_keys/busy_timeout)를 설정한 뒤, 필요하면 마이그레이션을
    /// 적용하고 반환한다. 매 호출마다 마이그레이션 여부를 다시 확인하지만
    /// <see cref="DatabaseMigrator.MigrateToLatest"/>는 이미 최신이면 즉시 반환하므로 비용이 낮다.
    /// </summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            // WAL: 읽기와 쓰기가 서로를 막지 않게 한다. busy_timeout: 동시 접근 시 즉시 실패 대신
            // 잠깐 재시도한다 (§143 - "손상된 DB를 자동으로 지우고 새로 만들지 않는다"는 원칙과는
            // 별개로, 잠깐의 잠금 경합은 실패가 아니라 재시도로 다뤄야 한다).
            pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
            pragma.ExecuteNonQuery();
        }

        DatabaseMigrator.MigrateToLatest(connection);
        return connection;
    }

    private static string ResolveDefaultDatabasePath()
    {
        var overridePath = Environment.GetEnvironmentVariable(DatabasePathEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDirectory = Path.Combine(localAppData, "CADWorkAssistant", "data");

        // Simulation Mode(FakeAutoCad)는 별도 파일을 쓴다 - 실제 업무 데이터와 절대 섞이면 안 된다
        // (Milestone 6 §150-151). AutoCadDiscoveryService가 이미 같은 환경변수로 Fake/Real AutoCAD를
        // 가른다 - 그 판단을 재사용한다(새 환경변수를 만들지 않는다).
        var isSimulationMode = Environment.GetEnvironmentVariable(SimulationModeEnvVar) == "1";
        var fileName = isSimulationMode ? "cadworkassistant.simulation.db" : "cadworkassistant.db";
        return Path.Combine(dataDirectory, fileName);
    }
}
