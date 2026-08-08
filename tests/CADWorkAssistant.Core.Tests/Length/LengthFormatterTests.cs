using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.Core.Tests.Length;

public class LengthFormatterTests
{
    [Fact]
    public void FormatMeters_RoundsToThreeDecimalPlacesByDefault()
    {
        var text = LengthFormatter.FormatMeters(255.940660);

        Assert.Equal("255.941", text);
    }

    [Fact]
    public void FormatMetersWithUnit_AppendsMeterSuffix()
    {
        var text = LengthFormatter.FormatMetersWithUnit(255.940660);

        Assert.Equal("255.941 m", text);
    }

    [Fact]
    public void FormatMeters_RoundsDownWhenBelowMidpoint()
    {
        var text = LengthFormatter.FormatMeters(49.204454);

        Assert.Equal("49.204", text);
    }

    [Fact]
    public void FormatMeters_Zero_FormatsAsZero()
    {
        var text = LengthFormatter.FormatMeters(0.0);

        Assert.Equal("0.000", text);
    }

    [Fact]
    public void FormatMeters_CustomDecimalPlaces_Honored()
    {
        var text = LengthFormatter.FormatMeters(255.940660, decimalPlaces: 1);

        Assert.Equal("255.9", text);
    }
}
