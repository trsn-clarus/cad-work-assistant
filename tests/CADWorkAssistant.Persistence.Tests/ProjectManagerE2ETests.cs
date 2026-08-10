using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Persistence.Tests;

/// <summary>
/// Milestone 13 §54 - Project Manager 전체 생명주기를 실제 파일 기반 SQLite로 검증한다: 프로젝트
/// 생성 → 도면/수량/출력물/활동 기록 → 재시작(같은 파일, 새 인스턴스) → 전부 복원되는지 확인 →
/// 누락된 도면 다시 연결 → 재시작 → 다시 연결한 경로가 그대로 남아 있는지 확인. 이 테스트 클래스는
/// 자기만의 <see cref="TestDatabaseFixture"/> 인스턴스(=자기만의 DB 파일)를 쓴다 - GetAllAsync처럼
/// 범위를 좁히지 않는 조회가 있어서 다른 테스트 클래스와 파일을 공유하면 안 된다.
/// </summary>
public sealed class ProjectManagerE2ETests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public ProjectManagerE2ETests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FullLifecycle_CreateDrawingsQuantitiesOutputsActivity_SurvivesRestart_ThenRelinkSurvivesRestartToo()
    {
        var projectId = Guid.NewGuid().ToString("N");
        var drawingFileId = Guid.NewGuid().ToString("N");

        // --- 1회차 실행: 프로젝트 생성 + 도면/수량/출력물/활동 기록 ---
        {
            var database = _fixture.CreateDatabase();
            var service = new ProjectDataService(database);
            var now = DateTimeOffset.UtcNow;

            var project = new Project(projectId, "서울의료원 옥상방수", now, now, now, client: "서울의료원", site: "서울특별시");
            var createActivity = new ActivityRecord(Guid.NewGuid().ToString("N"), projectId, "ProjectCreated", "프로젝트 생성", project.Name, now);
            await service.CreateProjectWithActivityAsync(project, createActivity);

            using (var connection = database.OpenConnection())
            {
                // 도면 2개(1개는 나중에 "누락"시킬 대상)
                await service.DrawingFiles.UpsertAsync(
                    new DrawingFile(drawingFileId, projectId, "School_Roof.dwg", @"C:\drawings\School_Roof.dwg", "mm", now, now),
                    connection);
                await service.DrawingFiles.UpsertAsync(
                    new DrawingFile(Guid.NewGuid().ToString("N"), projectId, "School_Detail.dwg", @"C:\drawings\School_Detail.dwg", "mm", now, now),
                    connection);
            }

            // 수량 2건 + 활동
            for (var i = 0; i < 2; i++)
            {
                var record = new QuantityRecord(
                    Guid.NewGuid().ToString("N"), "Length", "A-WALL", 1, 100m + i, "m", "School_Roof.dwg", now)
                {
                    ProjectId = projectId,
                };
                var activity = new ActivityRecord(Guid.NewGuid().ToString("N"), projectId, "QuantityAdded", $"Length 산출내역 추가 {i}", null, now);
                await service.AddQuantityRecordWithActivityAsync(record, activity);
            }

            // Excel/PDF/DWG Export 3종 + 활동(ProjectContextService.Add*ExportRecordAsync와 같은 트랜잭션 모양)
            using (var connection = database.OpenConnection())
            {
                foreach (var (exportType, targetFile, activityType) in new[]
                {
                    (ExportTypes.ExcelQuantity, @"C:\out\서울의료원_수량산출서.xlsx", "ExcelExportCompleted"),
                    (ExportTypes.PdfQuantityReport, @"C:\out\서울의료원_산출근거서.pdf", "PdfExportCompleted"),
                    (ExportTypes.DwgSelection, @"C:\out\실내마감표.dwg", "ExportCompleted"),
                })
                {
                    using var transaction = connection.BeginTransaction();
                    var exportRecord = new ExportRecord(
                        Guid.NewGuid().ToString("N"), projectId, sourceDrawing: "School_Roof.dwg", targetFile: targetFile,
                        objectCount: 2, description: "전체", createdAt: now, exportType: exportType);
                    var exportActivity = new ActivityRecord(Guid.NewGuid().ToString("N"), projectId, activityType, $"{exportType} 저장", targetFile, now);
                    await service.ExportRecords.InsertAsync(exportRecord, connection, transaction);
                    await service.Activity.InsertAsync(exportActivity, connection, transaction);
                    transaction.Commit();
                }
            }
        }

        // --- 재시작(같은 파일, 새 CadWorkAssistantDatabase 인스턴스) ---
        {
            var database = _fixture.CreateDatabase();
            var service = new ProjectDataService(database);
            using var connection = database.OpenConnection();

            var project = await service.Projects.FindByIdAsync(projectId, connection);
            var drawings = await service.DrawingFiles.GetByProjectAsync(projectId, connection);
            var quantities = await service.QuantityRecords.GetByProjectAsync(projectId, connection);
            var outputs = await service.ExportRecords.GetByProjectAsync(projectId, connection);
            var activity = await service.Activity.GetByProjectAsync(projectId, limit: 50, connection);
            var counts = await service.DrawingFiles.GetCountsByProjectAsync(connection);

            Assert.NotNull(project);
            Assert.Equal("서울의료원 옥상방수", project!.Name);
            Assert.Equal(2, drawings.Count);
            Assert.Equal(2, quantities.Count);
            Assert.Equal(3, outputs.Count);
            Assert.Contains(outputs, o => o.ExportType == ExportTypes.ExcelQuantity);
            Assert.Contains(outputs, o => o.ExportType == ExportTypes.PdfQuantityReport);
            Assert.Contains(outputs, o => o.ExportType == ExportTypes.DwgSelection);
            // ProjectCreated 1 + QuantityAdded 2 + Export 3 = 6
            Assert.Equal(6, activity.Count);
            Assert.Equal(2, counts[projectId]);
        }

        // --- 도면 하나를 다시 연결(§23-25 - 과거 QuantityRecord의 SourceDrawing snapshot은 그대로) ---
        {
            var database = _fixture.CreateDatabase();
            var service = new ProjectDataService(database);
            var relinkedAt = DateTimeOffset.UtcNow.AddMinutes(10);

            var relinkActivity = new ActivityRecord(Guid.NewGuid().ToString("N"), projectId, "DrawingFileRelinked", "도면 파일 다시 연결", "School_Roof_Final.dwg", relinkedAt);
            await service.RelinkDrawingFileAsync(drawingFileId, @"D:\moved\School_Roof_Final.dwg", "School_Roof_Final.dwg", "mm", relinkedAt, relinkActivity);
        }

        // --- 다시 재시작 - 새 경로가 그대로 남아 있는지, 과거 산출내역 SourceDrawing은 안 바뀌었는지 ---
        {
            var database = _fixture.CreateDatabase();
            var service = new ProjectDataService(database);
            using var connection = database.OpenConnection();

            var drawings = await service.DrawingFiles.GetByProjectAsync(projectId, connection);
            var relinked = drawings.Single(d => d.Id == drawingFileId);
            var quantities = await service.QuantityRecords.GetByProjectAsync(projectId, connection);
            var activity = await service.Activity.GetByProjectAsync(projectId, limit: 50, connection);

            Assert.Equal(@"D:\moved\School_Roof_Final.dwg", relinked.FullPath);
            Assert.Equal("School_Roof_Final.dwg", relinked.FileName);
            Assert.False(relinked.IsMissing);

            // 과거 수량의 SourceDrawing snapshot은 relink 이전 파일명 그대로 - relink가 과거 기록을
            // 다시 쓰지 않는다는 §25의 명시적 요구.
            Assert.All(quantities, q => Assert.Equal("School_Roof.dwg", q.SourceDrawing));

            Assert.Contains(activity, a => a.ActivityType == "DrawingFileRelinked");
        }
    }
}
