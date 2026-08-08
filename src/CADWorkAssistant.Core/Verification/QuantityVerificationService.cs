using System;
using System.Collections.Generic;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Parapet;
using CADWorkAssistant.Core.VerticalArea;

namespace CADWorkAssistant.Core.Verification;

/// <summary>
/// 저장된 QuantityRecord 하나를 검산한다 (Milestone 7). 목표는 "AI가 이상한 수량을 찾아준다"가 아니라,
/// 프로그램이 이미 알고 있는 수학적 사실(단위 변환 계수, 저장된 산식 입력값)과 CAD provenance를 이용해
/// 확실한 오류(<see cref="VerificationSeverity.Error"/>)와 단순한 이상 가능성
/// (<see cref="VerificationSeverity.Review"/>)을 구분하는 것이다(§163). 범용 Rule Engine이 아니다 -
/// 규칙마다 이름이 있는 private 메서드로 명확하게 구현한다(§12).
/// </summary>
public static class QuantityVerificationService
{
    /// <summary>규칙이 바뀌면 올린다 - 이미 저장된 Snapshot의 RuleSetVersion과 비교해 "재검산 필요"를
    /// 판단하는 데 쓰인다(§13, §50).</summary>
    public const int CurrentRuleSetVersion = 1;

    /// <summary>면적 대비 둘레가 눈에 띄게 긴 형상으로 판단하는 기준 - 정사각형(π/4 ≈ 0.785)보다
    /// 상당히 낮은 값이다. Error/Review를 유발하지 않고 Info 문구만 바꾼다(§41, §80 - 상수로 명시하고
    /// 문서화한다, `docs/QUANTITY_VERIFICATION.md` 참고).</summary>
    public const double CompactnessNoticeThreshold = 0.5;

    public static QuantityVerificationResult Verify(
        QuantityRecord record,
        QuantityVerificationContext context,
        DateTimeOffset checkedAt)
    {
        var checks = new List<VerificationCheckResult>();

        checks.Add(CheckFiniteValue(record));
        checks.Add(CheckPositiveQuantity(record));
        checks.Add(CheckUnitConsistency(record));
        AddIfNotNull(checks, CheckRawConversionConsistency(record));
        AddIfNotNull(checks, CheckFormulaRecompute(record));
        checks.Add(CheckProvenanceCompleteness(record));
        AddIfNotNull(checks, CheckDuplicateHandles(record, context));
        AddIfNotNull(checks, CheckPriorRecordComparison(record, context));
        AddIfNotNull(checks, CheckShapeSanity(record, context));

        return new QuantityVerificationResult(record.Id, CurrentRuleSetVersion, checkedAt, checks);
    }

    private static void AddIfNotNull(List<VerificationCheckResult> checks, VerificationCheckResult? check)
    {
        if (check is not null)
        {
            checks.Add(check);
        }
    }

    // --- Rule 1: Finite Value ---------------------------------------------------------------
    // QuantityRecord.Value/RawValue는 decimal이라 구조적으로 NaN/Infinity를 담을 수 없다(double과
    // 달리 decimal 타입 자체가 그런 값을 표현하지 못한다) - 그래도 §19가 명시적으로 요구하는 방어적
    // 검사이므로 규칙 자체는 남겨둔다. 실제로 이 Check는 항상 Pass다.
    private static VerificationCheckResult CheckFiniteValue(QuantityRecord record) =>
        new("FiniteValue", VerificationSeverity.Pass, "유효한 값",
            "저장된 값이 유효한 십진수입니다.");

    // --- Rule 2: Positive Quantity -----------------------------------------------------------
    private static VerificationCheckResult CheckPositiveQuantity(QuantityRecord record)
    {
        if (record.Value <= 0)
        {
            return new VerificationCheckResult("PositiveQuantity", VerificationSeverity.Error,
                "수량이 0 이하입니다.",
                $"저장된 값이 {record.Value} {record.Unit}입니다. 모든 측정 도구는 0보다 큰 값만 저장하도록 되어 있어, 이 값은 데이터 오류일 가능성이 높습니다.",
                $"value={record.Value}");
        }

        return new VerificationCheckResult("PositiveQuantity", VerificationSeverity.Pass, "값 정상", "저장된 값이 0보다 큽니다.");
    }

    // --- Rule 3: Unit Consistency ------------------------------------------------------------
    private static VerificationCheckResult CheckUnitConsistency(QuantityRecord record)
    {
        var expectedUnit = record.Type switch
        {
            "Length" => "m",
            "Area" => "m²",
            "VerticalArea" => "m²",
            "Parapet" => "m²",
            _ => (string?)null
        };

        if (expectedUnit is null)
        {
            return new VerificationCheckResult("UnitConsistency", VerificationSeverity.Info,
                "알 수 없는 측정 유형",
                $"'{record.Type}' 유형은 이 버전의 검산 엔진이 알지 못해 단위를 검증할 수 없습니다.");
        }

        if (record.Unit != expectedUnit)
        {
            return new VerificationCheckResult("UnitConsistency", VerificationSeverity.Error,
                "단위가 일치하지 않습니다.",
                $"'{record.Type}' 유형은 '{expectedUnit}' 단위를 사용해야 하지만 저장된 단위는 '{record.Unit}'입니다.",
                $"type={record.Type}, expectedUnit={expectedUnit}, actualUnit={record.Unit}");
        }

        return new VerificationCheckResult("UnitConsistency", VerificationSeverity.Pass, "단위 정상", $"'{record.Type}' 유형에 맞는 '{record.Unit}' 단위입니다.");
    }

    // --- Rule 4: Raw/Converted Consistency (Length, Area만) -----------------------------------
    // Vertical Area/Parapet의 RawValue는 "면적"이 아니라 "기준 길이"라 같은 방식으로 비교할 수 없다 -
    // 그 둘은 Rule 5(FormulaRecompute)가 대신 검산한다.
    private static VerificationCheckResult? CheckRawConversionConsistency(QuantityRecord record)
    {
        if (record.RawValue is not { } rawValue || record.SourceUnit is not { } sourceUnit)
        {
            return null;
        }

        double? factor = record.Type switch
        {
            "Length" => DrawingUnitDisplay.TryParseAbbreviation(sourceUnit, out var linearUnit)
                ? DrawingUnitConversion.MetersPerUnit(linearUnit)
                : null,
            "Area" => DrawingUnitDisplay.TryParseSquaredAbbreviation(sourceUnit, out var areaUnit)
                && DrawingUnitConversion.MetersPerUnit(areaUnit) is { } linearFactor
                    ? linearFactor * linearFactor
                    : null,
            _ => null
        };

        if (factor is null)
        {
            // Unitless/Other/인식 못하는 문자열 - 기계 검증이 불가능하다. Info로 소음을 내지 않는다 -
            // Unitless 도면은 저장 시점에 이미 UI가 "도면 단위가 설정되어 있지 않습니다"로 안내했다.
            return null;
        }

        // 원래 계산부(LengthWorkflowViewModel/AreaWorkflowViewModel)와 완전히 같은 순서로
        // double 연산 후 decimal로 캐스팅한다 - decimal 산술을 쓰면 double 기반으로 저장된 원본값과
        // 마지막 몇 자리에서 어긋날 수 있다(§23, tolerance로 흡수하되 연산 자체를 맞추는 게 우선).
        var expected = (decimal)((double)rawValue * factor.Value);

        if (!ToleranceEquals(expected, record.Value))
        {
            return new VerificationCheckResult("RawConversionConsistency", VerificationSeverity.Error,
                "저장값과 원본 단위 변환 결과가 일치하지 않습니다.",
                $"원본값 {rawValue} {sourceUnit} 기준 기대값은 {expected:0.######} {record.Unit}이지만 저장된 값은 {record.Value:0.######} {record.Unit}입니다.",
                $"raw={rawValue}, sourceUnit={sourceUnit}, factor={factor}, expected={expected}, actual={record.Value}");
        }

        return new VerificationCheckResult("RawConversionConsistency", VerificationSeverity.Pass, "원본값 변환 정상",
            $"원본값 {rawValue} {sourceUnit}이 저장된 값과 일치합니다.");
    }

    // --- Rule 5: Calculation Recompute (VerticalArea, Parapet만) ------------------------------
    private static VerificationCheckResult? CheckFormulaRecompute(QuantityRecord record)
    {
        if (record.Type == "VerticalArea")
        {
            var metadata = VerticalAreaCalculationMetadata.TryParse(record.CalculationMetadataJson);
            if (metadata is null)
            {
                return NotMachineVerifiable(record);
            }

            var recomputed = VerticalAreaCalculator.Calculate(
                metadata.SourceLengthMeters, metadata.HeightMeters, DrawingUnit.Meters, record.CreatedAt);
            var expected = (decimal)recomputed.AreaSquareMeters;

            return ToleranceEquals(expected, record.Value)
                ? new VerificationCheckResult("FormulaRecompute", VerificationSeverity.Pass, "산식 재계산 정상",
                    "저장된 구조화 입력값으로 다시 계산한 결과가 저장된 값과 일치합니다.")
                : new VerificationCheckResult("FormulaRecompute", VerificationSeverity.Error, "산식 재계산 결과가 일치하지 않습니다.",
                    $"기준 길이 {metadata.SourceLengthMeters:0.###} m × 높이 {metadata.HeightMeters:0.###} m로 다시 계산하면 {expected:0.###} m²이지만 저장된 값은 {record.Value:0.###} m²입니다.",
                    $"sourceLengthMeters={metadata.SourceLengthMeters}, heightMeters={metadata.HeightMeters}, expected={expected}, actual={record.Value}");
        }

        if (record.Type == "Parapet")
        {
            var metadata = ParapetCalculationMetadata.TryParse(record.CalculationMetadataJson);
            if (metadata is null)
            {
                return NotMachineVerifiable(record);
            }

            var input = new ParapetInput(
                metadata.SourceLengthMeters, metadata.HeightMeters, DrawingUnit.Meters,
                metadata.FaceMode, metadata.TopIncluded, metadata.TopWidthMeters, DrawingUnit.Meters);
            var recomputed = ParapetCalculator.Calculate(input, record.CreatedAt);
            var expected = (decimal)recomputed.TotalAreaSquareMeters;

            return ToleranceEquals(expected, record.Value)
                ? new VerificationCheckResult("FormulaRecompute", VerificationSeverity.Pass, "산식 재계산 정상",
                    "저장된 구조화 입력값으로 다시 계산한 결과가 저장된 값과 일치합니다.")
                : new VerificationCheckResult("FormulaRecompute", VerificationSeverity.Error, "산식 재계산 결과가 일치하지 않습니다.",
                    $"다시 계산하면 {expected:0.###} m²이지만 저장된 값은 {record.Value:0.###} m²입니다.",
                    $"metadata={metadata.ToJson()}, expected={expected}, actual={record.Value}");
        }

        return null;
    }

    /// <summary>구조화 메타데이터가 없는 과거 기록(§26) - 산식 문자열은 사람이 읽기 위한 것이라 다시
    /// 파싱해서 검산하지 않는다(§25). 산식 문자열이라도 있으면 Info, 그것마저 없으면 Review(§45).</summary>
    private static VerificationCheckResult NotMachineVerifiable(QuantityRecord record) =>
        string.IsNullOrEmpty(record.CalculationExpression)
            ? new VerificationCheckResult("FormulaRecompute", VerificationSeverity.Review, "계산 근거가 없습니다.",
                "구조화된 계산 입력값도, 산식 텍스트도 저장되어 있지 않아 이 기록이 어떻게 계산되었는지 확인할 수 없습니다.")
            : new VerificationCheckResult("FormulaRecompute", VerificationSeverity.Info, "자동 재계산 불가",
                "이전 버전에서 저장된 기록이라 구조화된 계산 입력값이 없습니다. 산식 텍스트는 있지만 자동으로 다시 계산하지는 않습니다.");

    // --- Rule 6: Provenance Completeness -------------------------------------------------------
    private static VerificationCheckResult CheckProvenanceCompleteness(QuantityRecord record)
    {
        // Length/Area는 MeasurementSource를 채우지 않는다(CAD 선택만 지원하므로 처음부터 null) -
        // null을 "CAD 선택으로 간주"로 취급한다. Manual은 Handle이 없는 게 정상이다(§30).
        var expectsHandles = record.MeasurementSource is null or "CadSelection" or "ExistingMeasurement";

        if (expectsHandles && record.ObjectHandles.Count == 0)
        {
            return new VerificationCheckResult("ProvenanceCompleteness", VerificationSeverity.Info,
                "CAD 객체 참조가 없습니다.",
                "CAD 선택 기반 측정으로 보이지만 원본 객체 Handle이 저장되어 있지 않습니다.");
        }

        return new VerificationCheckResult("ProvenanceCompleteness", VerificationSeverity.Pass, "출처 정보 정상",
            "이 측정값의 출처를 추적하는 데 필요한 정보가 있습니다.");
    }

    // --- Rule 7: Duplicate Source Handles ------------------------------------------------------
    private static VerificationCheckResult? CheckDuplicateHandles(QuantityRecord record, QuantityVerificationContext context)
    {
        var duplicate = context.FindExactDuplicate(record);
        if (duplicate is null)
        {
            return null;
        }

        return new VerificationCheckResult("DuplicateSourceHandles", VerificationSeverity.Review,
            "중복 가능성이 있는 산출내역입니다.",
            $"동일한 CAD 객체를 사용한 유사한 수량 기록이 이미 있습니다 ({duplicate.Type}, {duplicate.CreatedAt:yyyy-MM-dd HH:mm}). 의도적으로 여러 공종에 같은 객체를 쓴 것일 수도 있습니다.",
            $"duplicateOf={duplicate.Id}");
    }

    // --- Rule 8: Same Description Comparison ----------------------------------------------------
    private static VerificationCheckResult? CheckPriorRecordComparison(QuantityRecord record, QuantityVerificationContext context)
    {
        var previous = context.FindPreviousWithSameDescription(record);
        if (previous is null || previous.Value == 0)
        {
            return null;
        }

        var diff = record.Value - previous.Value;
        var percent = diff / previous.Value * 100m;
        var direction = diff >= 0 ? "증가" : "감소";

        // 절대 임계값으로 Review를 자동 선언하지 않는다(§35) - 비교 정보만 제공한다.
        return new VerificationCheckResult("PriorRecordComparison", VerificationSeverity.Info,
            "이전 기록과 값이 다릅니다.",
            $"같은 설명('{record.Description}')의 이전 기록({previous.CreatedAt:yyyy-MM-dd})보다 {Math.Abs(percent):0.#}% {direction}했습니다 ({previous.Value:0.###} → {record.Value:0.###} {record.Unit}).",
            $"previousId={previous.Id}, previousValue={previous.Value}, currentValue={record.Value}, percentChange={percent}");
    }

    // --- Rule 9: Area/Perimeter Shape Sanity (Compactness) ----------------------------------------
    private static VerificationCheckResult? CheckShapeSanity(QuantityRecord record, QuantityVerificationContext context)
    {
        var paired = context.FindShapeSanityPair(record);
        if (paired is null)
        {
            return null;
        }

        var areaRecord = record.Type == "Area" ? record : paired;
        var perimeterRecord = record.Type == "Length" ? record : paired;

        if (areaRecord.Value <= 0 || perimeterRecord.Value <= 0)
        {
            return null;
        }

        var area = (double)areaRecord.Value;
        var perimeter = (double)perimeterRecord.Value;
        var compactness = 4 * Math.PI * area / (perimeter * perimeter);

        // Compactness가 낮다고 오류가 아니다(§41) - Pass/Info만 쓰고 Review/Error는 절대 쓰지 않는다.
        if (compactness < CompactnessNoticeThreshold)
        {
            return new VerificationCheckResult("ShapeSanity", VerificationSeverity.Info,
                "면적 대비 둘레가 긴 형상입니다.",
                "복잡하거나 길쭉한 도형일 수 있습니다. 이 정보만으로 수량 오류라고 판단할 수 없습니다.",
                $"area={area}, perimeter={perimeter}, compactness={compactness:0.###}");
        }

        return new VerificationCheckResult("ShapeSanity", VerificationSeverity.Pass, "형상 참고",
            "면적 대비 둘레가 일반적인 범위입니다.", $"compactness={compactness:0.###}");
    }

    /// <summary>절대(a==b) 비교를 쓰지 않는다(§23) - 상대 오차 + 절대 하한을 함께 쓴다. 원본 계산부와
    /// 같은 순서로 double 연산을 재현하므로(§23 주석 참고) 실제로 필요한 허용오차는 매우 작다 - 그래도
    /// 부동소수점 연산 순서가 미묘하게 달라질 가능성에 대비해 여유를 둔다.</summary>
    private static bool ToleranceEquals(decimal expected, decimal actual)
    {
        var diff = Math.Abs(expected - actual);
        var tolerance = Math.Max(0.0005m, Math.Abs(expected) * 0.000001m);
        return diff <= tolerance;
    }
}
