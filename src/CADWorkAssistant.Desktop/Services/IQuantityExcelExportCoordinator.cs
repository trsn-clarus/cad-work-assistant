using System;
using System.Threading.Tasks;
using CADWorkAssistant.Documents.Excel;
using CADWorkAssistant.Documents.Reports;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>
/// Persistence -> Documents.Excel -> 저장된 파일까지의 조립 지점 (Milestone 9 §68, §163) -
/// ExcelExportViewModel은 이 인터페이스만 알고 ClosedXML/SQLite를 직접 다루지 않는다.
/// QuantityVerificationCoordinator(Milestone 7)와 같은 역할을 Excel Export에 대해 한다.
/// </summary>
public interface IQuantityExcelExportCoordinator
{
    /// <summary>미리보기/요약 텍스트용 - 파일을 쓰지 않는다. Project/QuantityRecord/Verification/
    /// Review를 전부 Persistence에서 새로 읽는다(§73-75, UI의 캐시된 ObservableCollection을 신뢰하지
    /// 않는다).</summary>
    Task<QuantityReportModel> BuildPreviewAsync(string projectId, ExcelExportOptions options);

    /// <summary>실제로 .xlsx를 저장하고, 성공하면 ExportRecord + Activity를 하나의 트랜잭션으로
    /// 남긴다(§79, §82-83). 미리보기와 마찬가지로 저장 직전에 다시 한 번 최신 snapshot을 읽는다.</summary>
    Task<ExcelExportResult> ExportAsync(string projectId, ExcelExportOptions options, string targetPath);
}
