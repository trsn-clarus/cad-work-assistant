using CADWorkAssistant.Core.Area;

namespace CADWorkAssistant.Core.Tests.Area;

public class AreaFormatterTests
{
    [Fact]
    public void FormatSquareMeters_RoundsToTwoDecimalPlacesByDefault()
    {
        var text = AreaFormatter.FormatSquareMeters(3102.426738);

        Assert.Equal("3,102.43", text);
    }

    [Fact]
    public void FormatSquareMetersWithUnit_AppendsSquareMeterSuffix()
    {
        var text = AreaFormatter.FormatSquareMetersWithUnit(3102.426738);

        Assert.Equal("3,102.43 m²", text);
    }

    [Fact]
    public void FormatSquareMeters_UsesThousandsSeparator()
    {
        var text = AreaFormatter.FormatSquareMeters(1520420.5);

        Assert.Equal("1,520,420.50", text);
    }

    [Fact]
    public void FormatSquareMeters_Zero_FormatsAsZero()
    {
        var text = AreaFormatter.FormatSquareMeters(0.0);

        Assert.Equal("0.00", text);
    }

    [Fact]
    public void FormatSquareMeters_CustomDecimalPlaces_Honored()
    {
        var text = AreaFormatter.FormatSquareMeters(3102.426738, decimalPlaces: 3);

        Assert.Equal("3,102.427", text);
    }
}
