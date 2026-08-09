using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Core.Tests.Plot;

public class PlotPdfDeviceSelectorTests
{
    [Fact]
    public void SelectBest_PreferredDevicePresent_ChoosesIt()
    {
        var devices = new[]
        {
            new CadPlotDeviceDto("HP LaserJet", isPdfCapable: false),
            new CadPlotDeviceDto("Some PDF Writer.pc3", isPdfCapable: true),
            new CadPlotDeviceDto("DWG To PDF.pc3", isPdfCapable: true),
        };

        var result = PlotPdfDeviceSelector.SelectBest(devices);

        Assert.NotNull(result);
        Assert.Equal("DWG To PDF.pc3", result!.Name);
    }

    [Fact]
    public void SelectBest_NoPreferredDevice_ChoosesFirstPdfCapable()
    {
        var devices = new[]
        {
            new CadPlotDeviceDto("HP LaserJet", isPdfCapable: false),
            new CadPlotDeviceDto("Some PDF Writer.pc3", isPdfCapable: true),
        };

        var result = PlotPdfDeviceSelector.SelectBest(devices);

        Assert.NotNull(result);
        Assert.Equal("Some PDF Writer.pc3", result!.Name);
    }

    [Fact]
    public void SelectBest_NoPdfCapableDevices_ReturnsNull()
    {
        var devices = new[] { new CadPlotDeviceDto("HP LaserJet", isPdfCapable: false) };

        var result = PlotPdfDeviceSelector.SelectBest(devices);

        Assert.Null(result);
    }

    [Fact]
    public void SelectBest_EmptyList_ReturnsNull()
    {
        var result = PlotPdfDeviceSelector.SelectBest(System.Array.Empty<CadPlotDeviceDto>());

        Assert.Null(result);
    }
}
