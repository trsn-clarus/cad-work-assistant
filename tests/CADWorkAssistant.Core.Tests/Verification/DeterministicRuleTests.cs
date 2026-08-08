using System.Linq;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Core.Tests.Verification;

/// <summary>Rule 1(Finite)/2(Positive)/3(Unit)/4(Raw-Converted) - Length/Area에 적용되는 결정적 검사.
/// 회귀값은 Milestone 2/3에서 이미 검증된 실무 예시(255.941 m, 3,102.43 m²)를 그대로 재사용한다.</summary>
public class DeterministicRuleTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:00:00+09:00");

    private static QuantityVerificationContext EmptyContext(QuantityRecord record) =>
        QuantityVerificationContext.Build(new[] { record });

    private static QuantityRecord LengthRecord(decimal value, decimal rawValue, string sourceUnit = "mm", string unit = "m") =>
        new("Q1", "Length", "A-WALL", 3, value, unit, "test.dwg", CreatedAt,
            rawValue: rawValue, sourceUnit: sourceUnit, objectHandles: new[] { "AB1", "AB2" });

    private static QuantityRecord AreaRecord(decimal value, decimal rawValue, string sourceUnit = "mm²", string unit = "m²") =>
        new("Q1", "Area", "A-FLOOR", 3, value, unit, "test.dwg", CreatedAt,
            rawValue: rawValue, sourceUnit: sourceUnit, objectHandles: new[] { "CD1" });

    [Fact]
    public void Verify_ValidLengthRecord_Passes()
    {
        // Milestone 2 §7 실무값: 255940.660 mm -> 255.940660 m
        var record = LengthRecord(255.940660m, 255940.660m);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Pass, result.OverallSeverity);
    }

    [Fact]
    public void Verify_ValidAreaRecord_Passes()
    {
        // Milestone 3 §33 실무값: 3,102,430,000 mm² -> 3,102.43 m²
        var record = AreaRecord(3102.43m, 3102430000m);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Pass, result.OverallSeverity);
    }

    [Fact]
    public void Verify_NegativeValue_ReturnsError()
    {
        var record = LengthRecord(-10m, -10000m);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Equal(VerificationSeverity.Error, result.OverallSeverity);
        Assert.Contains(result.Checks, c => c.RuleId == "PositiveQuantity" && c.Severity == VerificationSeverity.Error);
    }

    [Fact]
    public void Verify_ZeroValue_ReturnsError()
    {
        var record = LengthRecord(0m, 0m);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Contains(result.Checks, c => c.RuleId == "PositiveQuantity" && c.Severity == VerificationSeverity.Error);
    }

    [Fact]
    public void Verify_UnitMismatch_ReturnsError()
    {
        // Length인데 단위가 m²
        var record = LengthRecord(255.940660m, 255940.660m, unit: "m²");

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.Contains(result.Checks, c => c.RuleId == "UnitConsistency" && c.Severity == VerificationSeverity.Error);
    }

    [Fact]
    public void Verify_RawConversionMismatch_Length_ReturnsError()
    {
        // §86 Corrupt Record Test: Raw 255940.660 mm인데 저장값이 300 m (전혀 다른 값)
        var record = LengthRecord(300m, 255940.660m);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "RawConversionConsistency");
        Assert.Equal(VerificationSeverity.Error, check.Severity);
        Assert.Equal(VerificationSeverity.Error, result.OverallSeverity);
    }

    [Fact]
    public void Verify_RawConversionMismatch_Area_ReturnsError()
    {
        // §87: Raw 3,102,430,000 mm²인데 저장값이 4,500.00 m²
        var record = AreaRecord(4500.00m, 3102430000m);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        var check = result.Checks.Single(c => c.RuleId == "RawConversionConsistency");
        Assert.Equal(VerificationSeverity.Error, check.Severity);
    }

    [Fact]
    public void Verify_UnitlessSourceUnit_SkipsRawConversionCheck_NotError()
    {
        // Unitless 도면은 변환 계수가 없다 - 검산 불가로 조용히 넘어가야 하며 Error를 내면 안 된다.
        var record = LengthRecord(500000.00m, 500000.00m, sourceUnit: "Unitless");

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "RawConversionConsistency");
    }

    [Fact]
    public void Verify_MissingRawValue_SkipsRawConversionCheck()
    {
        var record = new QuantityRecord("Q1", "Length", "A-WALL", 1, 10m, "m", "test.dwg", CreatedAt);

        var result = QuantityVerificationService.Verify(record, EmptyContext(record), CreatedAt);

        Assert.DoesNotContain(result.Checks, c => c.RuleId == "RawConversionConsistency");
    }
}
