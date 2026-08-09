using CADWorkAssistant.Documents.Reports;

namespace CADWorkAssistant.Documents.Pdf;

/// <summary>
/// Milestone 10 §67-69 - Excel과 의미가 같은 옵션(Scope/IncludeReviewNotes/IncludeSourceDrawing)은
/// <see cref="IQuantityReportOptions"/>로 공유하고, PDF 고유의 Section 구성 옵션은 여기 따로 둔다 -
/// `ExcelExportOptions`를 억지로 재사용하지 않는다(§69).
/// </summary>
public sealed class PdfExportOptions : IQuantityReportOptions
{
    public QuantityExportScope Scope { get; init; } = QuantityExportScope.All;

    /// <summary>항목별 상세 블록에 산출식/원본값 변환 내역을 포함할지.</summary>
    public bool IncludeCalculationDetails { get; init; } = true;

    /// <summary>항목별 상세 블록에 자동 검산 결과를 포함할지.</summary>
    public bool IncludeVerification { get; init; } = true;

    public bool IncludeReviewNotes { get; init; } = true;

    public bool IncludeSourceDrawing { get; init; } = true;
}
