using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Core.Tests.Text;

public class TextContentValidatorTests
{
    [Theory]
    [InlineData("실내마감표")]
    [InlineData("A")]
    [InlineData("  A  ")]
    public void IsValid_NonEmptyContent_ReturnsTrue(string content)
    {
        Assert.True(TextContentValidator.IsValid(content));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValid_EmptyOrWhitespaceOrNull_ReturnsFalse(string? content)
    {
        Assert.False(TextContentValidator.IsValid(content));
    }

    [Fact]
    public void IsValid_DoesNotTrimSurroundingWhitespace_ForValidContent()
    {
        // §97: 사용자 입력을 임의로 trim하지 않는다 - 검증기는 판단만 하고 값을 바꾸지 않는다.
        const string content = "  옥상 방수공사  ";
        Assert.True(TextContentValidator.IsValid(content));
        Assert.Equal("  옥상 방수공사  ", content);
    }
}
