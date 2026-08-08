using CADWorkAssistant.Core.Drawing;

namespace CADWorkAssistant.Core.Tests.Drawing;

public class ExportFileNameServiceTests
{
    [Fact]
    public void SuggestFileName_KnownWorkOrderExample_MatchesExactly()
    {
        // §53, §100: "OO학교_건축.dwg" + "실내마감표" → "OO학교_건축_실내마감표.dwg"
        var result = ExportFileNameService.SuggestFileName("OO학교_건축.dwg", "실내마감표");

        Assert.Equal("OO학교_건축_실내마감표.dwg", result);
    }

    [Fact]
    public void SuggestFileName_EmptyDescription_ReturnsOriginalName()
    {
        var result = ExportFileNameService.SuggestFileName("OO학교_건축.dwg", "");

        Assert.Equal("OO학교_건축.dwg", result);
    }

    [Fact]
    public void SuggestFileName_DescriptionWithInvalidCharacters_SanitizesBeforeAppending()
    {
        var result = ExportFileNameService.SuggestFileName("Building.dwg", "1층: 평면도?");

        Assert.Equal("Building_1층_ 평면도_.dwg", result);
    }

    [Theory]
    [InlineData("실내마감표", "실내마감표")]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j", "a_b_c_d_e_f_g_h_i_j")]
    [InlineData("  spaced.  ", "spaced")]
    [InlineData("", "Export")]
    [InlineData("   ", "Export")]
    [InlineData("***", "___")]
    public void Sanitize_ProducesSafeFileNameFragment(string input, string expected)
    {
        Assert.Equal(expected, ExportFileNameService.Sanitize(input));
    }
}
