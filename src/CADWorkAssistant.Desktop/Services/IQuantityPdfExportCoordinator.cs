using System.Threading.Tasks;
using CADWorkAssistant.Documents.Pdf;
using CADWorkAssistant.Documents.Reports;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>
/// Persistence -> Documents.Pdf -> 저장된 파일까지의 조립 지점 (Milestone 10 §45) -
/// PdfExportViewModel은 이 인터페이스만 알고 PDFsharp/MigraDoc/SQLite를 직접 다루지 않는다.
/// IQuantityExcelExportCoordinator(Milestone 9)와 완전히 같은 모양이다 - 렌더러만 다르다.
/// </summary>
public interface IQuantityPdfExportCoordinator
{
    /// <summary>미리보기/요약 텍스트용 - 파일을 쓰지 않는다. Project/QuantityRecord/Verification/
    /// Review를 전부 Persistence에서 새로 읽는다(UI의 캐시된 ObservableCollection을 신뢰하지 않는다).</summary>
    Task<QuantityReportModel> BuildPreviewAsync(string projectId, PdfExportOptions options);

    /// <summary>실제로 .pdf를 저장하고, 성공하면 ExportRecord + Activity를 하나의 트랜잭션으로
    /// 남긴다. 미리보기와 마찬가지로 저장 직전에 다시 한 번 최신 snapshot을 읽는다.</summary>
    Task<PdfExportResult> ExportAsync(string projectId, PdfExportOptions options, string targetPath);
}
