using System.Linq;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Core.Tests.Verification;

/// <summary>Rule 6(Provenance)/7(Duplicate)/8(Prior Comparison)/9(Shape Sanity) - 같은 Project의
/// 다른 QuantityRecord를 참고해야 하는 검사. <see cref="QuantityVerificationContext"/>로 미리 색인한다.</summary>
public class ContextualRuleTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:00:00+09:00");

    [Fact]
    public void Verify_ManualSourceWithoutHandles_DoesNotFlagMissingProvenance()
    {
        // Manual 소스는 Handle이 없는 게 정상이다(§30) - Info를 내면 안 된다.
        var record = new QuantityRecord("Q1", "VerticalArea", "Manual", 0, 10m, "m²", "Manual Entry", CreatedAt,
            measurementSource: "Manual");
        var context = QuantityVerificationContext.Build(new[] { record });

        var result = QuantityVerificationService.Verify(record, context, CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "ProvenanceCompleteness" && c.Severity != VerificationSeverity.Pass);
    }

    [Fact]
    public void Verify_CadSelectionWithoutHandles_FlagsMissingProvenanceAsInfo()
    {
        var record = new QuantityRecord("Q1", "VerticalArea", "A-WALL", 0, 10m, "m²", "test.dwg", CreatedAt,
            measurementSource: "CadSelection", objectHandles: System.Array.Empty<string>());
        var context = QuantityVerificationContext.Build(new[] { record });

        var result = QuantityVerificationService.Verify(record, context, CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "ProvenanceCompleteness");
        Assert.Equal(VerificationSeverity.Info, check.Severity);
    }

    [Fact]
    public void Verify_ExactDuplicateHandleSet_FlagsAsReview()
    {
        var handles = new[] { "AB12", "AB13" };
        var first = new QuantityRecord("Q1", "Area", "A-FLOOR", 2, 100m, "m²", "test.dwg", CreatedAt,
            objectHandles: handles);
        var second = new QuantityRecord("Q2", "Area", "A-FLOOR", 2, 100m, "m²", "test.dwg", CreatedAt.AddMinutes(5),
            objectHandles: new[] { "AB13", "AB12" }); // 순서만 다르다 - 그래도 같은 집합
        var context = QuantityVerificationContext.Build(new[] { first, second });

        var result = QuantityVerificationService.Verify(second, context, CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "DuplicateSourceHandles");
        Assert.Equal(VerificationSeverity.Review, check.Severity);
        Assert.Equal(VerificationSeverity.Review, result.OverallSeverity);
    }

    [Fact]
    public void Verify_DifferentHandleSet_DoesNotFlagDuplicate()
    {
        var first = new QuantityRecord("Q1", "Area", "A-FLOOR", 2, 100m, "m²", "test.dwg", CreatedAt,
            objectHandles: new[] { "AB12" });
        var second = new QuantityRecord("Q2", "Area", "A-FLOOR", 2, 100m, "m²", "test.dwg", CreatedAt.AddMinutes(5),
            objectHandles: new[] { "CD99" });
        var context = QuantityVerificationContext.Build(new[] { first, second });

        var result = QuantityVerificationService.Verify(second, context, CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "DuplicateSourceHandles");
    }

    [Fact]
    public void Verify_DifferentType_SameHandles_DoesNotFlagDuplicate()
    {
        // Area/Length가 같은 Handle을 쓰는 건 정상(둘레+면적을 같은 폐합 도형에서 동시에 뽑는 경우) -
        // 중복 경고가 아니라 ShapeSanity 짝짓기 대상이다.
        var area = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 100m, "m²", "test.dwg", CreatedAt,
            objectHandles: new[] { "AB12" });
        var length = new QuantityRecord("Q2", "Length", "A-FLOOR", 1, 40m, "m", "test.dwg", CreatedAt,
            objectHandles: new[] { "AB12" });
        var context = QuantityVerificationContext.Build(new[] { area, length });

        var result = QuantityVerificationService.Verify(area, context, CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "DuplicateSourceHandles");
    }

    [Fact]
    public void Verify_PriorRecordWithSameDescription_ReportsInfoNotReview()
    {
        var previous = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 3102.43m, "m²", "test.dwg", CreatedAt) { Description = "옥상 바닥" };
        var current = new QuantityRecord("Q2", "Area", "A-FLOOR", 1, 3897.21m, "m²", "test.dwg", CreatedAt.AddDays(1)) { Description = "옥상 바닥" };
        var context = QuantityVerificationContext.Build(new[] { previous, current });

        var result = QuantityVerificationService.Verify(current, context, CreatedAt.AddDays(1));

        var check = result.Checks.Single(c => c.RuleId == "PriorRecordComparison");
        // §35: 절대 임계값으로 자동 Review 판정하지 않는다 - 값 차이가 커도 Info로 정보만 제공한다.
        Assert.Equal(VerificationSeverity.Info, check.Severity);
        Assert.Contains("%", check.Message);
    }

    [Fact]
    public void Verify_NoPriorRecordWithSameDescription_OmitsComparisonCheck()
    {
        var record = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 100m, "m²", "test.dwg", CreatedAt) { Description = "고유한 설명" };
        var context = QuantityVerificationContext.Build(new[] { record });

        var result = QuantityVerificationService.Verify(record, context, CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "PriorRecordComparison");
    }

    [Fact]
    public void Verify_ShapeSanity_CaseA_SmallerAreaLongerPerimeter_IsNotError()
    {
        // §44/§134 Case A: Area 3,100 m² / Perimeter 255 m - compactness ~0.6, 정상 범위로 Pass.
        var area = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 3100m, "m²", "test.dwg", CreatedAt,
            objectHandles: new[] { "AB1" });
        var length = new QuantityRecord("Q2", "Length", "A-FLOOR", 1, 255m, "m", "test.dwg", CreatedAt,
            objectHandles: new[] { "AB1" });
        var context = QuantityVerificationContext.Build(new[] { area, length });

        var result = QuantityVerificationService.Verify(area, context, CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "ShapeSanity" && c.Severity is VerificationSeverity.Error or VerificationSeverity.Review);
    }

    [Fact]
    public void Verify_ShapeSanity_CaseB_SmallerAreaEvenLongerPerimeter_IsNotError()
    {
        // Case B: Area 2,800 m² / Perimeter 295 m - 면적은 더 작은데 둘레는 더 길다. compactness가
        // Case A보다 낮아도 Error/Review가 되어서는 안 된다(§44의 핵심 요구).
        var area = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 2800m, "m²", "test.dwg", CreatedAt,
            objectHandles: new[] { "CD1" });
        var length = new QuantityRecord("Q2", "Length", "A-FLOOR", 1, 295m, "m", "test.dwg", CreatedAt,
            objectHandles: new[] { "CD1" });
        var context = QuantityVerificationContext.Build(new[] { area, length });

        var result = QuantityVerificationService.Verify(area, context, CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "ShapeSanity" && c.Severity is VerificationSeverity.Error or VerificationSeverity.Review);
        // 낮은 compactness는 Info로만 "참고 정보"를 준다.
        var check = result.Checks.SingleOrDefault(c => c.RuleId == "ShapeSanity");
        Assert.NotNull(check);
        Assert.Equal(VerificationSeverity.Info, check!.Severity);
    }

    [Fact]
    public void Verify_ShapeSanity_CircleLikeCompactShape_ReportsPass()
    {
        // 반지름 10m 원: 면적 ≈ 314.159, 둘레 ≈ 62.83 -> compactness = 1.0 (완전한 원)
        var area = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 314.159m, "m²", "test.dwg", CreatedAt,
            objectHandles: new[] { "EF1" });
        var length = new QuantityRecord("Q2", "Length", "A-FLOOR", 1, 62.832m, "m", "test.dwg", CreatedAt,
            objectHandles: new[] { "EF1" });
        var context = QuantityVerificationContext.Build(new[] { area, length });

        var result = QuantityVerificationService.Verify(area, context, CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "ShapeSanity");
        Assert.Equal(VerificationSeverity.Pass, check.Severity);
    }

    [Fact]
    public void Verify_NoPairedRecord_OmitsShapeSanityCheck()
    {
        var area = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 100m, "m²", "test.dwg", CreatedAt,
            objectHandles: new[] { "AB1" });
        var context = QuantityVerificationContext.Build(new[] { area });

        var result = QuantityVerificationService.Verify(area, context, CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "ShapeSanity");
    }

    [Fact]
    public void Verify_ShapeSanity_NeverProducesErrorOrReview_RegardlessOfCompactness()
    {
        // 극단적으로 길쭉한 형상(compactness가 매우 낮음)도 Error/Review가 되면 안 된다(§41, §81).
        var area = new QuantityRecord("Q1", "Area", "A-FLOOR", 1, 10m, "m²", "test.dwg", CreatedAt,
            objectHandles: new[] { "GH1" });
        var length = new QuantityRecord("Q2", "Length", "A-FLOOR", 1, 500m, "m", "test.dwg", CreatedAt,
            objectHandles: new[] { "GH1" });
        var context = QuantityVerificationContext.Build(new[] { area, length });

        var result = QuantityVerificationService.Verify(area, context, CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "ShapeSanity");
        Assert.True(check.Severity is VerificationSeverity.Pass or VerificationSeverity.Info);
    }
}
