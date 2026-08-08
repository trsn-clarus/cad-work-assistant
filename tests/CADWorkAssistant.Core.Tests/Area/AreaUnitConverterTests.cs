using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Tests.Area;

public class AreaUnitConverterTests
{
    [Fact]
    public void TryConvertToSquareMeters_Millimeters_DividesByOneMillion()
    {
        // §21: 1,000,000 mm² = 1 m² - 선형 계수를 한 번만 곱하면 틀린다.
        var ok = AreaUnitConverter.TryConvertToSquareMeters(1_000_000.0, DrawingUnit.Millimeters, out var squareMeters);

        Assert.True(ok);
        Assert.Equal(1.0, squareMeters, precision: 9);
    }

    [Fact]
    public void TryConvertToSquareMeters_Centimeters_DividesByTenThousand()
    {
        var ok = AreaUnitConverter.TryConvertToSquareMeters(10_000.0, DrawingUnit.Centimeters, out var squareMeters);

        Assert.True(ok);
        Assert.Equal(1.0, squareMeters, precision: 9);
    }

    [Fact]
    public void TryConvertToSquareMeters_Meters_ValueIsUnchanged()
    {
        var ok = AreaUnitConverter.TryConvertToSquareMeters(3102.43, DrawingUnit.Meters, out var squareMeters);

        Assert.True(ok);
        Assert.Equal(3102.43, squareMeters, precision: 6);
    }

    [Fact]
    public void TryConvertToSquareMeters_WorkedExample_MatchesKnownTotal()
    {
        // §33: 3,102,430,000 mm² → 3,102.43 m²
        var ok = AreaUnitConverter.TryConvertToSquareMeters(3_102_430_000.0, DrawingUnit.Millimeters, out var squareMeters);

        Assert.True(ok);
        Assert.Equal(3102.43, squareMeters, precision: 6);
    }

    [Fact]
    public void TryConvertToSquareMeters_Unitless_ReturnsFalseAndDoesNotGuess()
    {
        var ok = AreaUnitConverter.TryConvertToSquareMeters(500_000.0, DrawingUnit.Unitless, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryConvertToSquareMeters_Other_ReturnsFalse()
    {
        var ok = AreaUnitConverter.TryConvertToSquareMeters(500_000.0, DrawingUnit.Other, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryConvertToSquareMeters_Zero_ConvertsToZero()
    {
        var ok = AreaUnitConverter.TryConvertToSquareMeters(0.0, DrawingUnit.Millimeters, out var squareMeters);

        Assert.True(ok);
        Assert.Equal(0.0, squareMeters);
    }

    [Fact]
    public void TryConvertToSquareMeters_LargeValue_DoesNotOverflowOrLosePrecision()
    {
        var ok = AreaUnitConverter.TryConvertToSquareMeters(999_999_999_999.0, DrawingUnit.Millimeters, out var squareMeters);

        Assert.True(ok);
        Assert.Equal(999_999.999999, squareMeters, precision: 3);
    }
}
