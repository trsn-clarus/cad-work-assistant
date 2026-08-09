using CADWorkAssistant.Documents.Reports;

namespace CADWorkAssistant.Documents.Excel;

/// <summary>
/// Milestone 9 §66, §104-106 - 첫 화면에 필요한 정도로만 옵션을 둔다. font size/색상/여백 같은
/// 세부 조정은 넣지 않는다(§106) - 전부 useful default로 시작한다(§105). Scope/IncludeReviewNotes/
/// IncludeSourceDrawing은 PDF와 의미가 완전히 같아 <see cref="IQuantityReportOptions"/>로 공유하고
/// (Milestone 10 §68), Excel 고유의 시트 구성 옵션(IncludeCalculationBasis/IncludeVerificationDetail)은
/// 억지로 공유하지 않는다(§69).
/// </summary>
public sealed class ExcelExportOptions : IQuantityReportOptions
{
    public QuantityExportScope Scope { get; init; } = QuantityExportScope.All;

    /// <summary>Sheet 2(산출근거) 포함 여부.</summary>
    public bool IncludeCalculationBasis { get; init; } = true;

    /// <summary>Sheet 3(검산내역) 포함 여부.</summary>
    public bool IncludeVerificationDetail { get; init; } = true;

    /// <summary>검토 메모(사용자가 적은 자유 텍스트)를 포함할지 - 민감한 코멘트가 있을 수 있어
    /// 끌 수 있게 한다(§66).</summary>
    public bool IncludeReviewNotes { get; init; } = true;

    /// <summary>원본 DWG 파일명을 포함할지 - 전체 경로가 아니라 파일명만 다루지만, 그것도 회사
    /// 내부 프로젝트명을 드러낼 수 있어 끌 수 있게 한다(§23).</summary>
    public bool IncludeSourceDrawing { get; init; } = true;
}
