using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Core.Tests.Models;

public class QuantityTypeDisplayTests
{
    [Theory]
    [InlineData("Length", "길이")]
    [InlineData("Area", "면적")]
    [InlineData("VerticalArea", "수직면적")]
    [InlineData("Parapet", "파라펫")]
    public void DisplayName_MatchesKnownType(string type, string expectedDisplayName)
    {
        Assert.Equal(expectedDisplayName, QuantityTypeDisplay.DisplayName(type));
    }

    [Fact]
    public void DisplayName_UnknownType_ReturnsOriginal()
    {
        Assert.Equal("SomethingNew", QuantityTypeDisplay.DisplayName("SomethingNew"));
    }

    [Theory]
    [InlineData("Length", 3)]
    [InlineData("Area", 2)]
    [InlineData("VerticalArea", 3)]
    [InlineData("Parapet", 3)]
    public void DecimalPlaces_MatchesKnownType(string type, int expectedDecimalPlaces)
    {
        Assert.Equal(expectedDecimalPlaces, QuantityTypeDisplay.DecimalPlaces(type));
    }
}
