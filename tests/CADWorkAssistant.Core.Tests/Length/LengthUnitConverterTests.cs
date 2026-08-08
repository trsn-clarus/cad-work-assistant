using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.Core.Tests.Length;

public class LengthUnitConverterTests
{
    [Fact]
    public void TryConvertToMeters_Millimeters_ConvertsCorrectly()
    {
        var ok = LengthUnitConverter.TryConvertToMeters(255940.660, DrawingUnit.Millimeters, out var meters);

        Assert.True(ok);
        Assert.Equal(255.940660, meters, precision: 6);
    }

    [Fact]
    public void TryConvertToMeters_Centimeters_ConvertsCorrectly()
    {
        var ok = LengthUnitConverter.TryConvertToMeters(25594.0660, DrawingUnit.Centimeters, out var meters);

        Assert.True(ok);
        Assert.Equal(255.940660, meters, precision: 6);
    }

    [Fact]
    public void TryConvertToMeters_Meters_ValueIsUnchanged()
    {
        var ok = LengthUnitConverter.TryConvertToMeters(255.940660, DrawingUnit.Meters, out var meters);

        Assert.True(ok);
        Assert.Equal(255.940660, meters, precision: 6);
    }

    [Fact]
    public void TryConvertToMeters_Feet_ConvertsCorrectly()
    {
        var ok = LengthUnitConverter.TryConvertToMeters(10.0, DrawingUnit.Feet, out var meters);

        Assert.True(ok);
        Assert.Equal(3.048, meters, precision: 6);
    }

    [Fact]
    public void TryConvertToMeters_Unitless_ReturnsFalseAndDoesNotGuess()
    {
        var ok = LengthUnitConverter.TryConvertToMeters(500.0, DrawingUnit.Unitless, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryConvertToMeters_Other_ReturnsFalse()
    {
        var ok = LengthUnitConverter.TryConvertToMeters(500.0, DrawingUnit.Other, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryConvertToMeters_Zero_ConvertsToZero()
    {
        var ok = LengthUnitConverter.TryConvertToMeters(0.0, DrawingUnit.Millimeters, out var meters);

        Assert.True(ok);
        Assert.Equal(0.0, meters);
    }
}
