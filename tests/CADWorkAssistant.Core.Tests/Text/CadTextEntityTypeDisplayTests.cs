using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Core.Tests.Text;

public class CadTextEntityTypeDisplayTests
{
    [Fact]
    public void Label_SingleLine_ReturnsKoreanLabel()
    {
        Assert.Equal("단일행 문자", CadTextEntityTypeDisplay.Label(CadTextEntityType.SingleLine));
    }

    [Fact]
    public void Label_MultiLine_ReturnsKoreanLabel()
    {
        Assert.Equal("여러행 문자", CadTextEntityTypeDisplay.Label(CadTextEntityType.MultiLine));
    }
}
