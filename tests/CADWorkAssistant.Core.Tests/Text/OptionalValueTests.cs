using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Core.Tests.Text;

public class OptionalValueTests
{
    [Fact]
    public void None_HasValueIsFalse()
    {
        var value = OptionalValue<double>.None();

        Assert.False(value.HasValue);
    }

    [Fact]
    public void Some_HasValueIsTrue_AndValueIsPreserved()
    {
        var value = OptionalValue<string>.Some("A-TEXT");

        Assert.True(value.HasValue);
        Assert.Equal("A-TEXT", value.Value);
    }
}
