namespace CADWorkAssistant.Documents.Reports;

/// <summary>
/// Milestone 10 §68-69 - Excel/PDF Export 옵션 중 두 포맷에서 의미가 완전히 같은 것만 이 인터페이스로
/// 승격한다(Scope/IncludeReviewNotes/IncludeSourceDrawing - <see cref="QuantityReportModelBuilder"/>가
/// row 데이터 자체를 결정하는 데 쓰는 값들). Excel의 시트 구성(IncludeCalculationBasis/
/// IncludeVerificationDetail)과 PDF의 Section 구성(IncludeCalculationDetails/IncludeVerification)은
/// 렌더러마다 의미가 달라 억지로 공유하지 않는다(§69) - `ExcelExportOptions`/`PdfExportOptions`
/// 각자의 클래스에 그대로 남는다.
/// </summary>
public interface IQuantityReportOptions
{
    QuantityExportScope Scope { get; }

    bool IncludeReviewNotes { get; }

    bool IncludeSourceDrawing { get; }
}
