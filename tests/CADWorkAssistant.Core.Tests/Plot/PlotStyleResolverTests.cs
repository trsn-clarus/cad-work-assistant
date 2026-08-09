using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Core.Tests.Plot;

public class PlotStyleResolverTests
{
    [Fact]
    public void Resolve_KeepExisting_AlwaysAvailableWithNullStyleName()
    {
        var result = PlotStyleResolver.Resolve(
            CadPlotColorMode.KeepExisting, CadPlotStyleMode.ColorDependent,
            new[] { "monochrome.ctb" }, System.Array.Empty<string>());

        Assert.True(result.IsAvailable);
        Assert.Null(result.StyleSheetName);
    }

    [Fact]
    public void Resolve_MonochromeOnCtbDrawing_WithMonochromeAvailable_Resolves()
    {
        var result = PlotStyleResolver.Resolve(
            CadPlotColorMode.Monochrome, CadPlotStyleMode.ColorDependent,
            new[] { "acad.ctb", "monochrome.ctb", "Screening 50%.ctb" }, System.Array.Empty<string>());

        Assert.True(result.IsAvailable);
        Assert.Equal("monochrome.ctb", result.StyleSheetName);
    }

    [Fact]
    public void Resolve_MonochromeOnCtbDrawing_CaseInsensitiveMatch()
    {
        var result = PlotStyleResolver.Resolve(
            CadPlotColorMode.Monochrome, CadPlotStyleMode.ColorDependent,
            new[] { "Monochrome.CTB" }, System.Array.Empty<string>());

        Assert.True(result.IsAvailable);
        Assert.Equal("Monochrome.CTB", result.StyleSheetName);
    }

    [Fact]
    public void Resolve_MonochromeOnCtbDrawing_WithoutMonochromeAvailable_Unavailable()
    {
        var result = PlotStyleResolver.Resolve(
            CadPlotColorMode.Monochrome, CadPlotStyleMode.ColorDependent,
            new[] { "acad.ctb" }, System.Array.Empty<string>());

        Assert.False(result.IsAvailable);
        Assert.NotNull(result.UnavailableReason);
    }

    [Fact]
    public void Resolve_MonochromeOnStbDrawing_AlwaysUnavailable()
    {
        // §33: STB 도면에 monochrome.ctb를 억지로 적용하지 않는다 - CTB 목록에 monochrome.ctb가
        // 있어도 마찬가지다(STB 도면 자체에는 애초에 적용할 수 없다).
        var result = PlotStyleResolver.Resolve(
            CadPlotColorMode.Monochrome, CadPlotStyleMode.Named,
            new[] { "monochrome.ctb" }, new[] { "MyNamedStyle" });

        Assert.False(result.IsAvailable);
        Assert.NotNull(result.UnavailableReason);
    }
}
