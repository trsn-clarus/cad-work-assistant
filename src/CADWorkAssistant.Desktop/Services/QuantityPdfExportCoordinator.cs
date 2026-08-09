using System;
using System.Reflection;
using System.Threading.Tasks;
using CADWorkAssistant.Documents.Pdf;
using CADWorkAssistant.Documents.Reports;
using Serilog;

namespace CADWorkAssistant.Desktop.Services;

public sealed class QuantityPdfExportCoordinator : IQuantityPdfExportCoordinator
{
    private readonly IQuantityReportSnapshotService _snapshotService;
    private readonly IProjectContextService _projectContext;

    public QuantityPdfExportCoordinator(
        IQuantityReportSnapshotService snapshotService,
        IProjectContextService projectContext)
    {
        _snapshotService = snapshotService;
        _projectContext = projectContext;
    }

    public async Task<QuantityReportModel> BuildPreviewAsync(string projectId, PdfExportOptions options)
    {
        var model = await BuildModelAsync(projectId, options);
        return model;
    }

    public async Task<PdfExportResult> ExportAsync(string projectId, PdfExportOptions options, string targetPath)
    {
        var model = await BuildModelAsync(projectId, options);

        var builder = new QuantityPdfBuilder();
        // Excel(QuantityExcelExportCoordinator)과 같은 순서 원칙: PDF 파일 완전 생성 -> 성공 확인 ->
        // ExportRecord+Activity 저장. 파일 쓰기가 예외를 던지면 DB에는 아무것도 남기지 않는다.
        var result = builder.BuildAndSave(model, options, targetPath);

        try
        {
            var scopeText = options.Scope == QuantityExportScope.VerifiedOnly ? "검토 완료만" : "전체";
            await _projectContext.AddPdfExportRecordAsync(targetPath, result.RecordCount, $"{scopeText} · {result.RecordCount}건");
        }
        catch (Exception ex)
        {
            // 파일은 이미 성공적으로 생성됐다 - DB 기록 실패를 파일 생성 실패로 취급하지 않는다.
            Log.Error(ex, "Failed to record PDF export history for project {ProjectId}", projectId);
        }

        return result;
    }

    private async Task<QuantityReportModel> BuildModelAsync(string projectId, PdfExportOptions options)
    {
        // Milestone 10 §44: Excel Coordinator와 완전히 같은 스냅샷 서비스를 공유한다 - 두 포맷이
        // 같은 시점의 같은 데이터를 본다(Cross-format consistency).
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
