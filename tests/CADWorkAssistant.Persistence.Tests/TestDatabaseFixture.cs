using System;
using System.IO;

namespace CADWorkAssistant.Persistence.Tests;

/// <summary>
/// 실제 파일 기반 SQLite DB를 매 테스트마다 새 임시 경로에 만든다 (:memory: 대신) - Milestone 6
/// §143이 요구하는 "실제 파일 기반 테스트 전략"에 맞춘다. WAL 모드라 -wal/-shm 사이드카 파일도 같이
/// 정리한다.
/// </summary>
public sealed class TestDatabaseFixture : IDisposable
{
    public TestDatabaseFixture()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), "cwa-persistence-tests", $"{Guid.NewGuid():N}.db");
    }

    public string DatabasePath { get; }

    public CadWorkAssistantDatabase CreateDatabase() => new(DatabasePath);

    public void Dispose()
    {
        TryDelete(DatabasePath);
        TryDelete(DatabasePath + "-wal");
        TryDelete(DatabasePath + "-shm");
        TryDelete(DatabasePath + "-journal");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 테스트 정리 실패는 무시한다 - 실제 테스트 실패 여부와 무관하다.
        }
    }
}
