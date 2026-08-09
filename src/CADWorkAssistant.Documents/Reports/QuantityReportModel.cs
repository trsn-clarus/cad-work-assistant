using System;
using System.Collections.Generic;
using System.Linq;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Documents.Reports;

/// <summary>
/// "QuantityExportDocumentModel" (Milestone 9 §69-72) - QuantityRecord/QuantityVerificationResult/
/// QuantityReview/Project를 렌더러 전용 타입 없이 표현한 순수 데이터 모델이다. Milestone 9에서는
/// `Excel` 네임스페이스 안에 `QuantityWorkbookModel`이라는 Excel 종속적인 이름으로 있었지만,
/// Milestone 10 §4에서 실제로 PDF Renderer가 두 번째 소비자로 나타나면서 이 네임스페이스
/// (`Documents.Reports`)와 이름(QuantityReportModel/QuantityReportRow)으로 일반화했다 - 이름만
/// 바꾸는 리팩터링이 아니라, Excel(<see cref="CADWorkAssistant.Documents.Excel.QuantityWorkbookBuilder"/>)과
/// PDF(<see cref="CADWorkAssistant.Documents.Pdf.QuantityPdfBuilder"/>) 둘 다 이 모델을 그대로
/// 소비해 같은 record 순서/표시값/검산·검토 상태를 보장한다(Cross-format consistency, §144-148).
/// 모델 자체는 ClosedXML도 PDFsharp/MigraDoc도 전혀 모른다.
/// </summary>
public sealed class QuantityReportModel
{
    public required string ProjectName { get; init; }

    public string? Client { get; init; }

    public string? Site { get; init; }

    public string? Description { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public required string AppVersion { get; init; }

    public required IReadOnlyList<QuantityReportRow> Rows { get; init; }

    public int TotalCount => Rows.Count;

    public int VerifiedCount => Rows.Count(r => r.ReviewStatus == QuantityReviewStatus.Verified);

    public int NeedsReviewCount => Rows.Count(r => r.ReviewStatus == QuantityReviewStatus.NeedsReview);

    /// <summary>자동 검산이 Error인 건수 - 사용자가 Verified로 표시했더라도 그대로 센다(§136-137,
    /// 자동 경고를 숨기지 않는다).</summary>
    public int VerificationErrorCount => Rows.Count(r => r.VerificationSeverity == VerificationSeverity.Error);
}

/// <summary>수량 보고서 한 줄 - Excel의 Sheet 1/2/3과 PDF의 요약/상세/검산 Section이 같은 Row
/// 목록을 서로 다른 열/블록 조합으로 보여준다.</summary>
public sealed class QuantityReportRow
{
    /// <summary>1부터 시작하는 표시용 번호 - QuantityRecord.Id(GUID)는 노출하지 않는다.</summary>
    public required int Index { get; init; }

    public required string TypeDisplayName { get; init; }

    public required string Description { get; init; }

    /// <summary>사람이 읽는 산식 문자열 - Excel Formula(=...)나 PDF 마크업 명령으로 재해석하지
    /// 않는다(§15-16, §96).</summary>
    public string? CalculationExpression { get; init; }

    public required decimal Value { get; init; }

    public required string Unit { get; init; }

    public required int DecimalPlaces { get; init; }

    public decimal? RawValue { get; init; }

    public string? SourceUnit { get; init; }

    /// <summary>"CAD에서 선택"/"기존 측정값 재사용"/"직접 입력" - Length/Area는 이 개념이 없어 "-"(§20
    /// 산출근거 Sheet의 "측정방법" 컬럼).</summary>
    public required string MeasurementSourceDisplay { get; init; }

    /// <summary>파일명만 - 전체 경로는 담지 않는다(§23, §29, IncludeSourceDrawing이 false면 이 값
    /// 자체가 null이 되어 만들어진다).</summary>
    public string? SourceDrawingFileName { get; init; }

    public required int ObjectCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public VerificationSeverity? VerificationSeverity { get; init; }

    /// <summary>저장된 RuleSetVersion이 지금 엔진보다 낮다 - "재검산 필요"(§27, §135).</summary>
    public bool IsVerificationStale { get; init; }

    /// <summary>이미 "✓ 단위 일치" 형태로 만들어진 줄 목록 - Excel Sheet 3/PDF 검산 Section에서
    /// 줄바꿈으로 나열한다(§25, §23).</summary>
    public required IReadOnlyList<string> VerificationCheckLines { get; init; }

    public required QuantityReviewStatus ReviewStatus { get; init; }

    /// <summary>옵션의 IncludeReviewNotes가 false면 null.</summary>
    public string? ReviewNote { get; init; }
}
