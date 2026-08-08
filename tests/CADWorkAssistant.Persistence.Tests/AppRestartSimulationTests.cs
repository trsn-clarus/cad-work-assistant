using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;

namespace CADWorkAssistant.Persistence.Tests;

/// <summary>
/// Milestone 6의 명시적 요구: "앱을 껐다 켜도 작업이 남아 있어야 한다"를 :memory: DB가 아니라
/// 실제 파일로 검증한다. 첫 번째 <see cref="CadWorkAssistantDatabase"/>/커넥션을 완전히 버리고
/// 같은 파일 경로로 새 인스턴스를 만들어 "프로세스 재시작"을 흉내낸다 - 이 테스트가 실제로
/// 잡아낼 수 있는 버그: 마이그레이션이 매번 테이블을 지우고 새로 만드는 실수, 혹은 WAL 파일에만
/// 남고 메인 DB 파일에 checkpoint되지 않는 문제.
/// </summary>
public sealed class AppRestartSimulationTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public AppRestartSimulationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DataWrittenBeforeRestart_IsStillReadableAfterRestart()
    {
        var projectRepository = new SqliteProjectRepository();
        var quantityRepository = new SqliteQuantityRecordRepository();
        var projectId = Guid.NewGuid().ToString("N");

        // "앱 실행 1회차"
        {
            var database = _fixture.CreateDatabase();
            using var connection = database.OpenConnection();
            var now = DateTimeOffset.UtcNow;
            var project = new Project(projectId, "재시작 검증 프로젝트", now, now, now);
            await projectRepository.InsertAsync(project, connection);

            var record = new QuantityRecord(
                Guid.NewGuid().ToString("N"), "Length", "A-WALL", 4, 88.2m, "m", "restart-test.dwg", now)
            {
                ProjectId = projectId,
            };
            await quantityRepository.InsertAsync(record, connection);
        }

        // "앱 종료 후 재실행" - 완전히 새로운 CadWorkAssistantDatabase 인스턴스, 같은 파일 경로.
        {
            var database = _fixture.CreateDatabase();
            using var connection = database.OpenConnection();

            var project = await projectRepository.FindByIdAsync(projectId, connection);
            var records = await quantityRepository.GetByProjectAsync(projectId, connection);

            Assert.NotNull(project);
            Assert.Equal("재시작 검증 프로젝트", project!.Name);
            var record = Assert.Single(records);
            Assert.Equal(88.2m, record.Value);
        }
    }
}
