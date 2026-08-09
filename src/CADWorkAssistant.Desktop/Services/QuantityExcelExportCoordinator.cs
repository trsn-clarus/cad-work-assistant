using System;
using System.Reflection;
using System.Threading.Tasks;
using CADWorkAssistant.Documents.Excel;
using CADWorkAssistant.Persistence;
using Serilog;

namespace CADWorkAssistant.Desktop.Services;

public sealed class QuantityExcelExportCoordinator : IQuantityExcelExportCoordinator
{
    private readonly ProjectDataService _dataService;
    private readonly IQuantityVerificationCoordinator _verificationCoordinator;
    private readonly IProjectContextService _projectContext;

    public QuantityExcelExportCoordinator(
        ProjectDataService dataService,
        IQuantityVerificationCoordinator verificationCoordinator,
        IProjectContextService projectContext)
    {
        _dataService = dataService;
        _verificationCoordinator = verificationCoordinator;
        _projectContext = projectContext;
    }

    public async Task<QuantityWorkbookModel> BuildPreviewAsync(string projectId, ExcelExportOptions options)
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
            var scopeText = options.Scope == ExcelExportScope.VerifiedOnly ? "검토 완료만" : "전체";
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

    private async Task<QuantityWorkbookModel> BuildModelAsync(string projectId, ExcelExportOptions options)
    {
        using var connection = _dataService.Database.OpenConnection();
        var project = await _dataService.Projects.FindByIdAsync(projectId, connection)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");
        var records = await _dataService.QuantityRecords.GetByProjectAsync(projectId, connection);
        connection.Dispose();

        // Verification/Review는 이미 같은 정책(fresh read, 캐시 없음)으로 동작하는 기존 Coordinator를
        // 재사용한다(Milestone 7) - 검산 Snapshot 역직렬화 로직을 여기서 다시 만들지 않는다.
        var snapshotSet = await _verificationCoordinator.LoadForProjectAsync(projectId);

        return QuantityWorkbookModelBuilder.Build(
            project,
            records,
            snapshotSet.Verifications,
            snapshotSet.Reviews,
            options,
            DateTimeOffset.Now,
            AppVersion);
    }

    private static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "-";
}
