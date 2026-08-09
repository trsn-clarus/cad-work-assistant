using System;
using System.Reflection;
using System.Threading.Tasks;
using CADWorkAssistant.Documents.Excel;
using CADWorkAssistant.Documents.Reports;
using Serilog;

namespace CADWorkAssistant.Desktop.Services;

public sealed class QuantityExcelExportCoordinator : IQuantityExcelExportCoordinator
{
    private readonly IQuantityReportSnapshotService _snapshotService;
    private readonly IProjectContextService _projectContext;

    public QuantityExcelExportCoordinator(
        IQuantityReportSnapshotService snapshotService,
        IProjectContextService projectContext)
    {
        _snapshotService = snapshotService;
        _projectContext = projectContext;
    }

    public async Task<QuantityReportModel> BuildPreviewAsync(string projectId, ExcelExportOptions options)
    {
        var model = await BuildModelAsync(projectId, options);
        return model;
    }

    public async Task<ExcelExportResult> ExportAsync(string projectId, ExcelExportOptions options, string targetPath)
    {
        var model = await BuildModelAsync(projectId, options);

        var builder = new QuantityWorkbookBuilder();
        // §84: 실제 파일 쓰기(임시 파일 -> 검증 -> 원자적 교체)는 Documents가 전담한다 - 여기서는
        // 결과를 받아서 DB에 남기는 일만 한다. 파일 쓰기가 예외를 던지면 DB에는 아무것도 남기지
        // 않는다(§83, "1. Excel 파일 완전 생성 2. 성공 확인 3. ExportRecord+Activity 저장" 순서).
        var result = builder.BuildAndSave(model, options, targetPath);

        try
        {
            var scopeText = options.Scope == QuantityExportScope.VerifiedOnly ? "검토 완료만" : "전체";
            await _projectContext.AddExcelExportRecordAsync(targetPath, result.RecordCount, $"{scopeText} · {result.RecordCount}건");
        }
        catch (Exception ex)
        {
            // §83: 파일은 이미 성공적으로 생성됐다 - DB 기록 실패를 파일 생성 실패로 취급하지 않는다.
            // 사용자에게는 파일이 저장됐다고 알려야 하므로 여기서 다시 던지지 않는다.
            Log.Error(ex, "Failed to record Excel export history for project {ProjectId}", projectId);
        }

        return result;
    }

    private async Task<QuantityReportModel> BuildModelAsync(string projectId, ExcelExportOptions options)
    {
        // Milestone 10 §44: Project+QuantityRecord+Verification/Review 조회는 PDF Coordinator와
        // 완전히 같은 스냅샷 서비스를 공유한다 - 두 포맷이 같은 시점의 같은 데이터를 본다.
        var snapshot = await _snapshotService.LoadAsync(projectId);

        return QuantityReportModelBuilder.Build(
            snapshot.Project,
            snapshot.Records,
            snapshot.Verifications,
            snapshot.Reviews,
            options,
            DateTimeOffset.Now,
            AppVersion);
    }

    private static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "-";
}
