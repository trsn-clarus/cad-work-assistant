using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace CADWorkAssistant.Persistence.Migrations;

/// <summary>
/// 연결이 열릴 때마다 호출된다 (Milestone 6 §10-12). <c>PRAGMA user_version</c>과 등록된
/// <see cref="IMigration"/> 목록을 비교해서 미적용 항목만 순서대로, 하나의 트랜잭션 안에서 적용한다.
/// 전부 성공해야만 user_version을 올린다 - 중간에 실패하면 롤백되고 DB는 이전 버전 그대로 남는다
/// (파괴적 "지우고 새로 만들기" 금지, CLAUDE.md 절대 원칙 1 및 §11 손상 DB 자동 삭제 금지와 같은 원칙).
/// </summary>
internal static class DatabaseMigrator
{
    private static readonly IReadOnlyList<IMigration> Migrations = new IMigration[]
    {
        new Migration001InitialSchema(),
        new Migration002AddVerificationAndReview(),
    }.OrderBy(m => m.Version).ToArray();

    public static void MigrateToLatest(SqliteConnection connection)
    {
        var currentVersion = GetUserVersion(connection);
        var pending = Migrations.Where(m => m.Version > currentVersion).ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            var appliedVersion = currentVersion;
            foreach (var migration in pending)
            {
                migration.Apply(connection, transaction);
                appliedVersion = migration.Version;
            }

            SetUserVersion(connection, transaction, appliedVersion);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    private static void SetUserVersion(SqliteConnection connection, SqliteTransaction transaction, int version)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // PRAGMA는 파라미터 바인딩을 지원하지 않는다 - version은 내부 IMigration.Version(int literal)에서만
        // 오므로 문자열 결합이 안전하다(사용자 입력 아님).
        command.CommandText = $"PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
    }
}
