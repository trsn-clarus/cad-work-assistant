using System;
using System.Collections.Generic;
using System.Linq;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Documents.Excel;

/// <summary>
/// "QuantityExportDocumentModel" (Milestone 9 §69-72) - QuantityRecord/QuantityVerificationResult/
/// QuantityReview/Project를 Excel 전용 타입 없이 표현한 순수 데이터 모델이다. <see cref="QuantityWorkbookBuilder"/>
/// (ClosedXML)만 이 모델을 소비하지만, 모델 자체는 ClosedXML을 전혀 모른다 - 나중에 PDF Export를
/// 추가할 때(Milestone 10) 같은 모델을 그대로 재사용할 수 있게 하기 위해서다(§70, §174).
/// </summary>
public sealed class QuantityWorkbookModel
{
    public required string ProjectName { get; init; }

    public string? Client { get; init; }

    public string? Site { get; init; }

    public string? Description { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public required string AppVersion { get; init; }

    public required IReadOnlyList<QuantityWorkbookRow> Rows { get; init; }

    public int TotalCount => Rows.Count;

    public int VerifiedCount => Rows.Count(r => r.ReviewStatus == QuantityReviewStatus.Verified);

    public int NeedsReviewCount => Rows.Count(r => r.ReviewStatus == QuantityReviewStatus.NeedsReview);

    /// <summary>자동 검산이 Error인 건수 - 사용자가 Verified로 표시했더라도 그대로 센다(§136-137,
    /// 자동 경고를 숨기지 않는다).</summary>
    public int VerificationErrorCount => Rows.Count(r => r.VerificationSeverity == VerificationSeverity.Error);
}

/// <summary>수량산출서 한 줄 - Sheet 1/2/3이 같은 Row 목록을 서로 다른 열 조합으로 보여준다.</summary>
public sealed class QuantityWorkbookRow
{
    /// <summary>1부터 시작하는 표시용 번호 - QuantityRecord.Id(GUID)는 노출하지 않는다.</summary>
    public required int Index { get; init; }

    public required string TypeDisplayName { get; init; }

    public required string Description { get; init; }

    /// <summary>사람이 읽는 산식 문자열 - Excel Formula(=...)로 변환하지 않는다(§15-16).</summary>
    public string? CalculationExpression { get; init; }

    public required decimal Value { get; init; }

    public required string Unit { get; init; }

    public required int DecimalPlaces { get; init; }

    public decimal? RawValue { get; init; }

    public string? SourceUnit { get; init; }

    /// <summary>"CAD에서 선택"/"기존 측정값 재사용"/"직접 입력" - Length/Area는 이 개념이 없어 "-"(§20
    /// 산출근거 Sheet의 "측정방법" 컬럼).</summary>
    public required string MeasurementSourceDisplay { get; init; }

    /// <summary>파일명만 - 전체 경로는 담지 않는다(§23, ExcelExportOptions.IncludeSourceDrawing이
    /// false면 이 값 자체가 null이 되어 만들어진다).</summary>
    public string? SourceDrawingFileName { get; init; }

    public required int ObjectCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public VerificationSeverity? VerificationSeverity { get; init; }

    /// <summary>저장된 RuleSetVersion이 지금 엔진보다 낮다 - "재검산 필요"(§135).</summary>
    public bool IsVerificationStale { get; init; }

    /// <summary>이미 "✓ 단위 일치" 형태로 만들어진 줄 목록 - Sheet 3에서 줄바꿈으로 나열한다(§25).</summary>
    public required IReadOnlyList<string> VerificationCheckLines { get; init; }

    public required QuantityReviewStatus ReviewStatus { get; init; }

    /// <summary>ExcelExportOptions.IncludeReviewNotes가 false면 null.</summary>
    public string? ReviewNote { get; init; }
}
