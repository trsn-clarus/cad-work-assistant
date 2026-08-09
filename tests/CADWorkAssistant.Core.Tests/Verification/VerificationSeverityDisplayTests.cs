using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Core.Tests.Verification;

public class VerificationSeverityDisplayTests
{
    [Fact]
    public void Glyph_Null_ReturnsDash()
    {
        Assert.Equal("—", VerificationSeverityDisplay.Glyph(null));
    }

    [Theory]
    [InlineData(VerificationSeverity.Pass, "✓")]
    [InlineData(VerificationSeverity.Review, "!")]
    [InlineData(VerificationSeverity.Error, "×")]
    [InlineData(VerificationSeverity.Info, "?")]
    public void Glyph_MatchesSeverity(VerificationSeverity severity, string expectedGlyph)
    {
        Assert.Equal(expectedGlyph, VerificationSeverityDisplay.Glyph(severity));
    }

    [Fact]
    public void Label_Null_ReturnsNotYetVerified()
    {
        Assert.Equal("미검산", VerificationSeverityDisplay.Label(null));
    }

    [Theory]
    [InlineData(VerificationSeverity.Pass, "검산 완료")]
    [InlineData(VerificationSeverity.Review, "확인 필요")]
    [InlineData(VerificationSeverity.Error, "오류")]
    [InlineData(VerificationSeverity.Info, "검산 불가")]
    public void Label_MatchesSeverity(VerificationSeverity severity, string expectedLabel)
    {
        Assert.Equal(expectedLabel, VerificationSeverityDisplay.Label(severity));
    }
}
