using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Core.Tests.Plot;

public class PlotOrientationResolverTests
{
    [Fact]
    public void Resolve_ExplicitPortrait_IgnoresWindow()
    {
        var window = new CadPlotWindowDto(0, 0, 1000, 100); // very wide

        var result = PlotOrientationResolver.Resolve(CadPlotOrientation.Portrait, window);

        Assert.Equal(CadPlotOrientation.Portrait, result);
    }

    [Fact]
    public void Resolve_ExplicitLandscape_IgnoresWindow()
    {
        var window = new CadPlotWindowDto(0, 0, 100, 1000); // very tall

        var result = PlotOrientationResolver.Resolve(CadPlotOrientation.Landscape, window);

        Assert.Equal(CadPlotOrientation.Landscape, result);
    }

    [Fact]
    public void Resolve_AutoWideWindow_ReturnsLandscape()
    {
        var window = new CadPlotWindowDto(0, 0, 20000, 8000);

        var result = PlotOrientationResolver.Resolve(CadPlotOrientation.Auto, window);

        Assert.Equal(CadPlotOrientation.Landscape, result);
    }

    [Fact]
    public void Resolve_AutoTallWindow_ReturnsPortrait()
    {
        var window = new CadPlotWindowDto(0, 0, 8000, 20000);

        var result = PlotOrientationResolver.Resolve(CadPlotOrientation.Auto, window);

        Assert.Equal(CadPlotOrientation.Portrait, result);
    }

    [Fact]
    public void Resolve_AutoNoWindow_DefaultsToPortrait()
    {
        var result = PlotOrientationResolver.Resolve(CadPlotOrientation.Auto, window: null);

        Assert.Equal(CadPlotOrientation.Portrait, result);
    }

    [Fact]
    public void Resolve_AutoSquareWindow_DeterministicDefault()
    {
        var window = new CadPlotWindowDto(0, 0, 10000, 10000);

        var result = PlotOrientationResolver.Resolve(CadPlotOrientation.Auto, window);

        Assert.Equal(CadPlotOrientation.Portrait, result);
    }
}
