using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.VerticalArea;

namespace CADWorkAssistant.Core.Tests.VerticalArea;

public class VerticalAreaCalculatorTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:35:00+09:00");

    [Fact]
    public void Calculate_RealWorldCaseA_MatchesKnownWorkOrderExample()
    {
        // Milestone 4 §57 Case A: 255940.660 mm 길이 (Milestone 2 §7과 같은 값), 높이 0.10 m
        var lengthMeters = 255940.660 * 0.001;

        var result = VerticalAreaCalculator.Calculate(lengthMeters, 0.10, DrawingUnit.Meters, CreatedAt);

        Assert.Equal(25.594066, result.AreaSquareMeters, precision: 6);
        Assert.Equal("25.594 m²", AreaFormatter.FormatSquareMetersWithUnit(result.AreaSquareMeters, decimalPlaces: 3));
    }

    [Fact]
    public void Calculate_RealWorldCaseB_MatchesKnownWorkOrderExample()
    {
        // §57 Case B: 295141.237 mm, 높이 0.10 m
        var lengthMeters = 295141.237 * 0.001;

        var result = VerticalAreaCalculator.Calculate(lengthMeters, 0.10, DrawingUnit.Meters, CreatedAt);

        Assert.Equal(29.5141237, result.AreaSquareMeters, precision: 7);
        Assert.Equal("29.514 m²", AreaFormatter.FormatSquareMetersWithUnit(result.AreaSquareMeters, decimalPlaces: 3));
    }

    [Fact]
    public void Calculate_HeightInMillimeters_NormalizesToMeters()
    {
        // §58: 100 mm -> 0.1 m
        var result = VerticalAreaCalculator.Calculate(10.0, 100.0, DrawingUnit.Millimeters, CreatedAt);

        Assert.Equal(0.1, result.HeightMeters, precision: 9);
        Assert.Equal(1.0, result.AreaSquareMeters, precision: 9);
    }

    [Fact]
    public void Calculate_HeightInCentimeters_NormalizesToMeters()
    {
        // §58: 10 cm -> 0.1 m
        var result = VerticalAreaCalculator.Calculate(10.0, 10.0, DrawingUnit.Centimeters, CreatedAt);

        Assert.Equal(0.1, result.HeightMeters, precision: 9);
    }

    [Fact]
    public void Calculate_HeightInMeters_IsUnchanged()
    {
        var result = VerticalAreaCalculator.Calculate(10.0, 0.1, DrawingUnit.Meters, CreatedAt);

        Assert.Equal(0.1, result.HeightMeters, precision: 9);
    }

    [Fact]
    public void Calculate_DoesNotRoundIntermediateValues()
    {
        // §60: 255.940660 x 0.10을 미리 반올림한 255.941 x 0.10으로 계산하면 안 된다.
        var result = VerticalAreaCalculator.Calculate(255.940660, 0.10, DrawingUnit.Meters, CreatedAt);

        Assert.NotEqual(25.5941, result.AreaSquareMeters);
        Assert.Equal(25.594066, result.AreaSquareMeters, precision: 6);
    }

    [Fact]
    public void Validate_PositiveHeight_ReturnsValid()
    {
        Assert.Equal(VerticalAreaValidation.Valid, VerticalAreaCalculator.Validate(0.1));
    }

    [Fact]
    public void Validate_ZeroHeight_ReturnsHeightNotPositive()
    {
        Assert.Equal(VerticalAreaValidation.HeightNotPositive, VerticalAreaCalculator.Validate(0.0));
    }

    [Fact]
    public void Validate_NegativeHeight_ReturnsHeightNotPositive()
    {
        Assert.Equal(VerticalAreaValidation.HeightNotPositive, VerticalAreaCalculator.Validate(-1.0));
    }

    [Fact]
    public void Calculate_ZeroHeight_ThrowsBecauseCallerMustValidateFirst()
    {
        Assert.Throws<ArgumentException>(() => VerticalAreaCalculator.Calculate(10.0, 0.0, DrawingUnit.Meters, CreatedAt));
    }

    [Fact]
    public void Calculate_NegativeHeight_Throws()
    {
        Assert.Throws<ArgumentException>(() => VerticalAreaCalculator.Calculate(10.0, -0.5, DrawingUnit.Meters, CreatedAt));
    }

    [Fact]
    public void Calculate_LargeLength_ProducesAccurateArea()
    {
        var result = VerticalAreaCalculator.Calculate(10_000.0, 2.5, DrawingUnit.Meters, CreatedAt);

        Assert.Equal(25_000.0, result.AreaSquareMeters, precision: 3);
    }

    [Fact]
    public void Calculate_ManualLengthSource_WorksTheSameAsCadLength()
    {
        // Core는 길이가 CAD에서 왔는지 수동 입력인지 모른다 - 정규화된 meters 값만 받는다.
        var result = VerticalAreaCalculator.Calculate(32.118, 1.0, DrawingUnit.Meters, CreatedAt);

        Assert.Equal(32.118, result.AreaSquareMeters, precision: 6);
    }

    [Fact]
    public void Calculate_UnitlessHeight_Throws()
    {
        Assert.Throws<ArgumentException>(() => VerticalAreaCalculator.Calculate(10.0, 1.0, DrawingUnit.Unitless, CreatedAt));
    }
}
