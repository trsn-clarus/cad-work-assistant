using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Core.Tests.Plot;

public class PlotPaperMatcherTests
{
    [Fact]
    public void FindMatch_ExactA4Portrait_Matches()
    {
        var media = new[] { new CadPlotMediaDto("ISO_A4_(210.00_x_297.00_MM)", "A4", 210, 297) };

        var result = PlotPaperMatcher.FindMatch(media, CadPaperSizeCatalog.A4);

        Assert.NotNull(result);
        Assert.Equal("ISO_A4_(210.00_x_297.00_MM)", result!.CanonicalName);
    }

    [Fact]
    public void FindMatch_A4ReportedAsLandscape_StillMatches()
    {
        // §21: 매처는 두 방향 모두 비교한다 - 장치가 297x210으로 보고해도 A4로 인식해야 한다.
        var media = new[] { new CadPlotMediaDto("A4_LANDSCAPE", "A4", 297, 210) };

        var result = PlotPaperMatcher.FindMatch(media, CadPaperSizeCatalog.A4);

        Assert.NotNull(result);
    }

    [Fact]
    public void FindMatch_A3Dimensions_DoesNotMatchA4()
    {
        var media = new[] { new CadPlotMediaDto("ISO_A3", "A3", 297, 420) };

        var result = PlotPaperMatcher.FindMatch(media, CadPaperSizeCatalog.A4);

        Assert.Null(result);
    }

    [Fact]
    public void FindMatch_WithinTolerance_Matches()
    {
        // §22: printable area 반올림으로 인한 미세 오차.
        var media = new[] { new CadPlotMediaDto("A4_ROUNDED", "A4", 210.0 + PlotPaperMatcher.ToleranceMm, 297.0) };

        var result = PlotPaperMatcher.FindMatch(media, CadPaperSizeCatalog.A4);

        Assert.NotNull(result);
    }

    [Fact]
    public void FindMatch_BeyondTolerance_DoesNotMatch()
    {
        var media = new[] { new CadPlotMediaDto("NOT_A4", "?", 210.0 + PlotPaperMatcher.ToleranceMm + 5.0, 297.0) };

        var result = PlotPaperMatcher.FindMatch(media, CadPaperSizeCatalog.A4);

        Assert.Null(result);
    }

    [Fact]
    public void FindMatch_NoCandidates_ReturnsNull()
    {
        var result = PlotPaperMatcher.FindMatch(System.Array.Empty<CadPlotMediaDto>(), CadPaperSizeCatalog.A3);

        Assert.Null(result);
    }

    [Fact]
    public void FindMatch_UnsupportedMediaOnly_ReturnsNull()
    {
        var media = new[] { new CadPlotMediaDto("LETTER", "Letter", 215.9, 279.4) };

        var result = PlotPaperMatcher.FindMatch(media, CadPaperSizeCatalog.A3);

        Assert.Null(result);
    }
}
