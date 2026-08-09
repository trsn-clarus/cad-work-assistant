using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Core.Tests.Text;

public class CadColorDtoTests
{
    [Fact]
    public void Equals_SameModeAndAciIndex_AreEqual()
    {
        var a = new CadColorDto(CadColorMode.Aci, 1, 0, 0, 0, "Red");
        var b = new CadColorDto(CadColorMode.Aci, 1, 0, 0, 0, "Different display name");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentAciIndex_AreNotEqual()
    {
        var a = new CadColorDto(CadColorMode.Aci, 1, 0, 0, 0, "Red");
        var b = new CadColorDto(CadColorMode.Aci, 2, 0, 0, 0, "Yellow");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ByLayerAndByBlock_AreNotEqual()
    {
        Assert.NotEqual(CadColorPalette.ByLayer, CadColorPalette.ByBlock);
    }

    [Fact]
    public void Equals_DifferentTrueColorRgb_AreNotEqual()
    {
        var a = new CadColorDto(CadColorMode.TrueColor, 0, 255, 0, 0, "Custom");
        var b = new CadColorDto(CadColorMode.TrueColor, 0, 0, 255, 0, "Custom");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void CommonAci_HasSevenEntries_MatchingAutoCadStandardColors()
    {
        Assert.Equal(7, CadColorPalette.CommonAci.Count);
    }
}
