using System.Linq;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Parapet;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Core.VerticalArea;

namespace CADWorkAssistant.Core.Tests.Verification;

/// <summary>Rule 5(Formula Recompute) - Vertical Area/Parapet은 구조화 메타데이터로 저장된 결과를
/// 실제 Calculator(VerticalAreaCalculator/ParapetCalculator)로 다시 계산해 비교한다. 회귀값은
/// Milestone 4/§82-83의 실무 예시를 그대로 재사용한다. ObjectHandles를 채워 ProvenanceCompleteness
/// Check가 소음(Info)을 내지 않게 한다 - 이 파일의 관심사는 오직 FormulaRecompute다.</summary>
public class FormulaRecomputeTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:00:00+09:00");
    private static readonly string[] Handles = { "AB1" };

    private static QuantityVerificationContext EmptyContext(QuantityRecord record) =>
        QuantityVerificationContext.Build(new[] { record });

    [Fact]
    public void Verify_VerticalArea_CaseA_MatchesKnownWorkOrderExample()
    {
        // 255940.660 mm × 0.10 m = 25.594066 m² (Milestone 4 §57 Case A)
        var sourceLengthMeters = 255940.660 * 0.001;
        var metadata = new VerticalAreaCalculationMetadata(sourceLengthMeters, 0.10);
        var record = new QuantityRecord("Q1", "VerticalArea", "A-WALL", 3, 25.594066m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles, calculationMetadataJson: metadata.ToJson());

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Pass, result.OverallSeverity);
        Assert.Contains(result.Checks, c => c.RuleId == "FormulaRecompute" && c.Severity == VerificationSeverity.Pass);
    }

    [Fact]
    public void Verify_VerticalArea_CaseB_MatchesKnownWorkOrderExample()
    {
        // 295141.237 mm × 0.10 m = 29.5141237 m² (§57 Case B)
        var sourceLengthMeters = 295141.237 * 0.001;
        var metadata = new VerticalAreaCalculationMetadata(sourceLengthMeters, 0.10);
        var record = new QuantityRecord("Q1", "VerticalArea", "A-WALL", 3, 29.5141237m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles, calculationMetadataJson: metadata.ToJson());

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Pass, result.OverallSeverity);
    }

    [Fact]
    public void Verify_Parapet_BothFacesWithTop_MatchesKnownWorkOrderExample()
    {
        // 32.118m × 1m × 2(양면) + 32.118m × 0.15m(상부) = 69.0537 m² (§82, §158)
        var metadata = new ParapetCalculationMetadata(32.118, 1.0, ParapetFaceMode.Both, topIncluded: true, topWidthMeters: 0.15);
        var record = new QuantityRecord("Q1", "Parapet", "A-WALL", 1, 69.0537m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles, calculationMetadataJson: metadata.ToJson());

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Pass, result.OverallSeverity);
    }

    [Fact]
    public void Verify_Parapet_SingleFaceNoTop_MatchesFormula()
    {
        var metadata = new ParapetCalculationMetadata(10.0, 0.5, ParapetFaceMode.Single, topIncluded: false, topWidthMeters: 0);
        var record = new QuantityRecord("Q1", "Parapet", "A-WALL", 1, 5.0m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles, calculationMetadataJson: metadata.ToJson());

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Pass, result.OverallSeverity);
    }

    [Fact]
    public void Verify_VerticalArea_ValueDoesNotMatchMetadata_ReturnsError()
    {
        var metadata = new VerticalAreaCalculationMetadata(255.941, 0.10);
        // 올바른 값은 25.5941인데 완전히 다른 값(99.0)을 저장 - DB 손상/수동 편집 흉내
        var record = new QuantityRecord("Q1", "VerticalArea", "A-WALL", 3, 99.0m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles, calculationMetadataJson: metadata.ToJson());

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Error, result.OverallSeverity);
        Assert.Contains(result.Checks, c => c.RuleId == "FormulaRecompute" && c.Severity == VerificationSeverity.Error);
    }

    [Fact]
    public void Verify_Parapet_ValueDoesNotMatchMetadata_ReturnsError()
    {
        var metadata = new ParapetCalculationMetadata(32.118, 1.0, ParapetFaceMode.Both, topIncluded: true, topWidthMeters: 0.15);
        var record = new QuantityRecord("Q1", "Parapet", "A-WALL", 1, 10.0m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles, calculationMetadataJson: metadata.ToJson());

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Error, result.OverallSeverity);
    }

    [Fact]
    public void Verify_VerticalArea_MissingMetadataButHasExpression_ReturnsInfo()
    {
        var record = new QuantityRecord("Q1", "VerticalArea", "A-WALL", 3, 25.594066m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles, calculationExpression: "255.941 m × 0.100 m = 25.594 m²");

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "FormulaRecompute");
        Assert.Equal(VerificationSeverity.Info, check.Severity);
    }

    [Fact]
    public void Verify_Parapet_MissingMetadataAndExpression_ReturnsReview()
    {
        var record = new QuantityRecord("Q1", "Parapet", "A-WALL", 1, 69.0537m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "FormulaRecompute");
        Assert.Equal(VerificationSeverity.Review, check.Severity);
        Assert.Equal(VerificationSeverity.Review, result.OverallSeverity);
    }

    [Fact]
    public void Verify_MissingMetadata_NeverReturnsError()
    {
        // §26/§88: 구조화 metadata가 없는 과거 데이터를 Crash/Error로 처리하지 않는다.
        var record = new QuantityRecord("Q1", "Parapet", "A-WALL", 1, 69.0537m, "m²", "test.dwg", CreatedAt,
            objectHandles: Handles);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "FormulaRecompute" && c.Severity == VerificationSeverity.Error);
    }
}
