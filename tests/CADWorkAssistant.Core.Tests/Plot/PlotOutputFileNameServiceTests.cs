using System;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Core.Tests.Plot;

public class PlotOutputFileNameServiceTests
{
    private static readonly DateTime FixedDate = new(2026, 8, 9);

    [Fact]
    public void SuggestFileName_WindowScope_UsesAreaOutputLabel()
    {
        var name = PlotOutputFileNameService.SuggestFileName(
            "학교_건축도면.dwg", CadPlotScope.Window, layoutName: null,
            CadPaperSizeCatalog.A3, CadPlotColorMode.Monochrome, FixedDate);

        Assert.Equal("학교_건축도면_영역출력_A3_흑백_20260809.pdf", name);
    }

    [Fact]
    public void SuggestFileName_LayoutScope_UsesLayoutName()
    {
        var name = PlotOutputFileNameService.SuggestFileName(
            "School_Roof.dwg", CadPlotScope.CurrentLayout, layoutName: "Layout1",
            CadPaperSizeCatalog.A4, CadPlotColorMode.KeepExisting, FixedDate);

        Assert.Equal("School_Roof_Layout1_A4_컬러_20260809.pdf", name);
    }

    [Fact]
    public void SuggestFileName_KoreanLayoutName_SanitizedNotBroken()
    {
        var name = PlotOutputFileNameService.SuggestFileName(
            "도면.dwg", CadPlotScope.CurrentLayout, layoutName: "배치1",
            CadPaperSizeCatalog.A4, CadPlotColorMode.KeepExisting, FixedDate);

        Assert.Equal("도면_배치1_A4_컬러_20260809.pdf", name);
    }

    [Fact]
    public void SuggestFileName_InvalidCharactersInLayoutName_Sanitized()
    {
        var name = PlotOutputFileNameService.SuggestFileName(
            "Drawing.dwg", CadPlotScope.CurrentLayout, layoutName: "A/B:C",
            CadPaperSizeCatalog.A4, CadPlotColorMode.KeepExisting, FixedDate);

        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain(':', name);
    }

    [Fact]
    public void SuggestFileName_NullLayoutName_FallsBackToLayoutLabel()
    {
        var name = PlotOutputFileNameService.SuggestFileName(
            "Drawing.dwg", CadPlotScope.CurrentLayout, layoutName: null,
            CadPaperSizeCatalog.A4, CadPlotColorMode.KeepExisting, FixedDate);

        Assert.Equal("Drawing_Layout_A4_컬러_20260809.pdf", name);
    }

    [Fact]
    public void SuggestFileName_LongDrawingName_DoesNotThrow()
    {
        var longName = new string('가', 100) + ".dwg";

        var name = PlotOutputFileNameService.SuggestFileName(
            longName, CadPlotScope.Window, layoutName: null,
            CadPaperSizeCatalog.A3, CadPlotColorMode.Monochrome, FixedDate);

        Assert.EndsWith("_영역출력_A3_흑백_20260809.pdf", name);
    }
}
