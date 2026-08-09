using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Core.Tests.Text;

public class TextHeightValidatorTests
{
    [Theory]
    [InlineData(250.0)]
    [InlineData(0.001)]
    [InlineData(10000.0)]
    public void IsValid_PositiveHeight_ReturnsTrue(double height)
    {
        Assert.True(TextHeightValidator.IsValid(height));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsValid_NonPositiveOrNonFinite_ReturnsFalse(double height)
    {
        Assert.False(TextHeightValidator.IsValid(height));
    }
}
