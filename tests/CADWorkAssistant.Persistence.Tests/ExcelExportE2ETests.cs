using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Documents.Excel;
using ClosedXML.Excel;

namespace CADWorkAssistant.Persistence.Tests;

/// <summary>
/// Milestone 9 §183-188: 실제 SQLite 파일에 Project/QuantityRecord/QuantityVerificationSnapshot/
/// QuantityReview를 실제로 저장한 뒤, Desktop의 QuantityExcelExportCoordinator가 하는 것과 정확히
/// 같은 순서(Repository에서 fresh read -> QuantityWorkbookModelBuilder -> QuantityWorkbookBuilder)로
/// 진짜 .xlsx까지 만들어 검증한다. Desktop 프로젝트 자체는 여기서 참조하지 않는다 - Coordinator는
/// 이미 검증된 조각들(Repository, QuantityVerificationService 스냅샷 직렬화 규칙, Documents Excel
/// Builder)을 조립하기만 하므로, 그 조립 순서를 이 테스트가 그대로 재현하는 것으로 충분하다.
/// </summary>
public sealed class ExcelExportE2ETests : IClassFixture<TestDatabaseFixture>, IDisposable
{
    private readonly TestDatabaseFixture _fixture;
    private readonly string _tempDir;

    public ExcelExportE2ETests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _tempDir = Path.Combine(Path.GetTempPath(), "cwa-excel-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task FullWorkflow_ProjectThroughExcel_ProducesCorrectWorkbook()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var now = DateTimeOffset.Parse("2026-08-09T10:00:00+09:00");

        string projectId;
        using (var connection = database.OpenConnection())
        {
            var project = new Project(
                Guid.NewGuid().ToString("N"), "서울의료원 옥상 방수공사", now, now, now,
                client: "서울의료원", site: "서울 OO구");
            await service.Projects.InsertAsync(project, connection);
            projectId = project.Id;

            var length = new QuantityRecord(
                Guid.NewGuid().ToString("N"), "Length", "A-WALL", 4, 255.940660m, "m", @"C:\CWA_B1_FloorPlan.dwg", now,
                calculationExpression: "125.331 + 81.405 + 49.205 = 255.941 m")
            { ProjectId = projectId };
            var area = new QuantityRecord(
                Guid.NewGuid().ToString("N"), "Area", "A-ROOF", 3, 3102.43m, "m²", @"C:\School_Roof.dwg", now.AddMinutes(1))
            { ProjectId = projectId, Description = "파라펫 내·외측 및 상부면" };
            var verticalArea = new QuantityRecord(
                Guid.NewGuid().ToString("N"), "VerticalArea", "A-PARAPET", 1, 25.594066m, "m²", @"C:\School_Roof.dwg", now.AddMinutes(2),
                calculationExpression: "255.941 x 0.100 = 25.594 m²")
            { ProjectId = projectId };
            var parapet = new QuantityRecord(
                Guid.NewGuid().ToString("N"), "Parapet", "A-PARAPET", 1, 69.0537m, "m²", @"C:\School_Roof.dwg", now.AddMinutes(3))
            { ProjectId = projectId };

            foreach (var record in new[] { length, area, verticalArea, parapet })
            {
                await service.QuantityRecords.InsertAsync(record, connection);
            }

            // 4건 중 2건 Verified, 1건 NeedsReview, 1건 Unreviewed(§184) - Parapet은 검산 Error인데도
            // 사용자가 Verified로 표시한 경우(§185)로 만든다.
            await service.QuantityVerifications.UpsertAsync(SnapshotOf(projectId, length.Id, VerificationSeverity.Pass, "단위 일치"), connection);
            await service.QuantityVerifications.UpsertAsync(SnapshotOf(projectId, area.Id, VerificationSeverity.Pass, "단위 일치"), connection);
            await service.QuantityVerifications.UpsertAsync(SnapshotOf(projectId, verticalArea.Id, VerificationSeverity.Review, "동일한 CAD 객체를 사용한 유사한 수량 기록이 있습니다"), connection);
            await service.QuantityVerifications.UpsertAsync(SnapshotOf(projectId, parapet.Id, VerificationSeverity.Error, "저장값과 원본 단위 변환 결과가 일치하지 않습니다"), connection);

            await service.QuantityReviews.UpsertAsync(ReviewOf(projectId, length.Id, QuantityReviewStatus.Verified, "확인 완료"), connection);
            await service.QuantityReviews.UpsertAsync(ReviewOf(projectId, area.Id, QuantityReviewStatus.NeedsReview, null), connection);
            // verticalArea: Unreviewed(기록 없음)
            await service.QuantityReviews.UpsertAsync(ReviewOf(projectId, parapet.Id, QuantityReviewStatus.Verified, "현장 확인 완료. ㄱ자 평면으로 정상."), connection);
        }

        // ---- All scope ----
        var allPath = Path.Combine(_tempDir, "all-scope.xlsx");
        await ExportAsync(service, projectId, new ExcelExportOptions { Scope = ExcelExportScope.All }, allPath);

        using (var workbook = new XLWorkbook(allPath))
        {
            Assert.Equal(new[] { "수량산출서", "산출근거", "검산내역", "프로젝트정보" }, workbook.Worksheets.Select(w => w.Name));

            var sheet = workbook.Worksheet("수량산출서");
            var usedRows = sheet.RangeUsed()!.RowCount();
            // Header block(제목+프로젝트정보+요약+빈줄) + 헤더 행 + 4개 데이터 행 - 정확한 행 번호를
            // 못박지 않고 "4개 데이터 행이 전부 있다"만 확인한다(레이아웃이 조금 바뀌어도 안 깨지게).
            Assert.True(usedRows >= 4);

            var allText = string.Join(" ", sheet.CellsUsed().Select(c => c.GetString()));
            Assert.Contains("서울의료원 옥상 방수공사", allText);
            Assert.Contains("길이", allText);
            Assert.Contains("면적", allText);
            Assert.Contains("수직면적", allText);
            Assert.Contains("파라펫", allText);
            Assert.Contains("125.331 + 81.405 + 49.205 = 255.941 m", allText);
            Assert.Contains("✓ 검산 완료", allText);
            Assert.Contains("! 확인 필요", allText);
            Assert.Contains("× 오류", allText);
            Assert.Contains("검토 완료", allText);
            Assert.Contains("확인 필요", allText);
            Assert.Contains("미검토", allText);

            var verificationSheet = workbook.Worksheet("검산내역");
            var verificationText = string.Join(" ", verificationSheet.CellsUsed().Select(c => c.GetString()));
            Assert.Contains("현장 확인 완료. ㄱ자 평면으로 정상.", verificationText);

            var infoSheet = workbook.Worksheet("프로젝트정보");
            var infoText = string.Join(" ", infoSheet.CellsUsed().Select(c => c.GetString()));
            Assert.Contains("서울의료원", infoText);
        }

        // ---- Verified-only scope (§184: 2 Verified만 포함) ----
        var verifiedOnlyPath = Path.Combine(_tempDir, "verified-only.xlsx");
        var verifiedOnlyResult = await ExportAsync(service, projectId, new ExcelExportOptions { Scope = ExcelExportScope.VerifiedOnly }, verifiedOnlyPath);
        Assert.Equal(2, verifiedOnlyResult.RecordCount);

        using (var workbook = new XLWorkbook(verifiedOnlyPath))
        {
            var sheet = workbook.Worksheet("수량산출서");
            var allText = string.Join(" ", sheet.CellsUsed().Select(c => c.GetString()));
            // Parapet은 Verified인데 자동 검산은 Error - Verified-only 범위에 포함되면서도 Error가
            // 그대로 보여야 한다(§136-137, 자동 경고를 숨기지 않는다).
            Assert.Contains("파라펫", allText);
            Assert.Contains("× 오류", allText);
            // Area(NeedsReview)와 VerticalArea(Unreviewed)는 제외되어야 한다.
            Assert.DoesNotContain("파라펫 내·외측 및 상부면", allText);
        }

        // ---- Export history recorded ----
        using var verifyConnection = database.OpenConnection();
        var exportRecords = await service.ExportRecords.GetByProjectAsync(projectId, verifyConnection);
        Assert.Equal(2, exportRecords.Count);
        Assert.All(exportRecords, r => Assert.Equal(ExportTypes.ExcelQuantity, r.ExportType));
    }

    [Fact]
    public async Task Export_ZeroQuantityProject_ProducesEmptyWorkbook()
    {
        var database = _fixture.CreateDatabase();
        var service = new ProjectDataService(database);
        var now = DateTimeOffset.UtcNow;

        string projectId;
        using (var connection = database.OpenConnection())
        {
            var project = new Project(Guid.NewGuid().ToString("N"), "빈 프로젝트", now, now, now);
            await service.Projects.InsertAsync(project, connection);
            projectId = project.Id;
        }

        var path = Path.Combine(_tempDir, "empty-project.xlsx");
        var result = await ExportAsync(service, projectId, new ExcelExportOptions(), path);

        Assert.Equal(0, result.RecordCount);
        using var workbook = new XLWorkbook(path);
        Assert.NotEmpty(workbook.Worksheets);
    }

    private static async Task<ExcelExportResult> ExportAsync(
        ProjectDataService service, string projectId, ExcelExportOptions options, string targetPath)
    {
        Project project;
        System.Collections.Generic.IReadOnlyList<QuantityRecord> records;
        System.Collections.Generic.IReadOnlyList<QuantityVerificationSnapshot> snapshots;
        System.Collections.Generic.IReadOnlyList<QuantityReview> reviews;

        using (var connection = service.Database.OpenConnection())
        {
            project = await service.Projects.FindByIdAsync(projectId, connection)
                ?? throw new InvalidOperationException("Project not found");
            records = await service.QuantityRecords.GetByProjectAsync(projectId, connection);
            snapshots = await service.QuantityVerifications.GetByProjectAsync(projectId, connection);
            reviews = await service.QuantityReviews.GetByProjectAsync(projectId, connection);
        }

        var verifications = snapshots.ToDictionary(s => s.QuantityRecordId, ToResult);
        var reviewMap = reviews.ToDictionary(r => r.QuantityRecordId);

        var model = QuantityWorkbookModelBuilder.Build(
            project, records, verifications, reviewMap, options, DateTimeOffset.Now, "0.9.0");

        var result = new QuantityWorkbookBuilder().BuildAndSave(model, options, targetPath);

        // Coordinator가 하는 것과 같은 순서: 파일이 성공적으로 만들어진 뒤에만 이력을 남긴다(§83).
        var scopeText = options.Scope == ExcelExportScope.VerifiedOnly ? "검토 완료만" : "전체";
        var exportRecord = new ExportRecord(
            Guid.NewGuid().ToString("N"), projectId, null, targetPath, result.RecordCount,
            $"{scopeText} · {result.RecordCount}건", DateTimeOffset.UtcNow, ExportTypes.ExcelQuantity);
        using (var connection = service.Database.OpenConnection())
        {
            await service.ExportRecords.InsertAsync(exportRecord, connection);
        }

        return result;
    }

    private static QuantityVerificationSnapshot SnapshotOf(string projectId, string recordId, VerificationSeverity severity, string title)
    {
        var checks = new List<VerificationCheckResult> { new("Rule", severity, title, "message") };
        var json = JsonSerializer.Serialize(checks, IpcJson.Options);
        return new QuantityVerificationSnapshot(
            Guid.NewGuid().ToString("N"), projectId, recordId, severity,
            QuantityVerificationService.CurrentRuleSetVersion, DateTimeOffset.UtcNow, json);
    }

    private static QuantityReview ReviewOf(string projectId, string recordId, QuantityReviewStatus status, string? note) =>
        new(Guid.NewGuid().ToString("N"), projectId, recordId, status, note, DateTimeOffset.UtcNow);

    private static QuantityVerificationResult ToResult(QuantityVerificationSnapshot snapshot)
    {
        var checks = JsonSerializer.Deserialize<List<VerificationCheckResult>>(snapshot.ChecksJson, IpcJson.Options)
            ?? new List<VerificationCheckResult>();
        return new QuantityVerificationResult(snapshot.QuantityRecordId, snapshot.RuleSetVersion, snapshot.CheckedAt, checks);
    }
}
