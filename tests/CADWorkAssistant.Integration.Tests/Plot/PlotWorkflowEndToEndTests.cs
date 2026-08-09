using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Plot;

/// <summary>
/// Milestone 11 §104-106 - GetPlotCapabilities → AcquirePlotWindow → PlotDrawingPdf 전체 흐름을
/// 실제 Named Pipe로 검증한다. FakeAutoCad는 진짜 AutoCAD Plot 엔진을 흉내내지 않는다(§97-98) -
/// "IPC/파일 배관"이 끝까지 동작하는지, 그리고 Preset Resolution(§39)이 실제 Capability 응답을
/// 근거로 올바르게 동작하는지를 검증한다. 실제 PDF 픽셀 정확성은 여기서 주장하지 않는다(Milestone
/// 11B, docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md).
/// </summary>
public class PlotWorkflowEndToEndTests
{
    [Fact]
    public async Task FullFlow_WindowScope_CapabilitiesThenWindowThenPlot_Succeeds()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotSuccess", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var capabilitiesResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetPlotCapabilities, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(capabilitiesResponse.Success);
        var capabilities = capabilitiesResponse.DeserializePayload<PlotCapabilitiesResponse>()!;
        Assert.Contains(capabilities.Devices, d => d.IsPdfCapable);
        Assert.NotEmpty(capabilities.Media);

        var windowResponse = await client.SendRequestAsync(
            IpcMessageTypes.AcquirePlotWindow, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(windowResponse.Success);
        var window = windowResponse.DeserializePayload<AcquirePlotWindowResponse>()!.Window;
        Assert.True(window.Width > 0);
        Assert.True(window.Height > 0);

        var targetPath = Path.Combine(Path.GetTempPath(), $"cwa-plot-test-{Guid.NewGuid():n}.pdf");
        try
        {
            var plotResponse = await client.SendRequestAsync(
                IpcMessageTypes.PlotDrawingPdf,
                new PlotDrawingPdfRequest(
                    CadPlotScope.Window, layoutName: null, window,
                    CadPaperSizeCatalog.A4.Name, CadPlotOrientation.Auto, CadPlotColorMode.Monochrome, targetPath),
                IpcProtocol.RequestTimeoutMs,
                CancellationToken.None);

            Assert.True(plotResponse.Success);
            var result = plotResponse.DeserializePayload<PlotDrawingPdfResponse>()!;
            Assert.Equal(targetPath, result.OutputFile);
            Assert.True(File.Exists(targetPath));
            Assert.Equal("monochrome.ctb", result.ResolvedStyleSheet);
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [Fact]
    public async Task CurrentLayoutScope_DoesNotRequireWindow_Succeeds()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotSuccess", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var targetPath = Path.Combine(Path.GetTempPath(), $"cwa-plot-layout-test-{Guid.NewGuid():n}.pdf");
        try
        {
            var plotResponse = await client.SendRequestAsync(
                IpcMessageTypes.PlotDrawingPdf,
                new PlotDrawingPdfRequest(
                    CadPlotScope.CurrentLayout, layoutName: "Layout1", window: null,
                    CadPaperSizeCatalog.A3.Name, CadPlotOrientation.Landscape, CadPlotColorMode.KeepExisting, targetPath),
                IpcProtocol.RequestTimeoutMs,
                CancellationToken.None);

            Assert.True(plotResponse.Success);
            var result = plotResponse.DeserializePayload<PlotDrawingPdfResponse>()!;
            Assert.Equal(CadPlotOrientation.Landscape, result.ResolvedOrientation);
            Assert.True(File.Exists(targetPath));
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [Fact]
    public async Task NoPdfDeviceCapabilities_ReportsNoPdfCapableDevice()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotCapabilitiesNoPdfDevice", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.GetPlotCapabilities, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.True(response.Success);
        var capabilities = response.DeserializePayload<PlotCapabilitiesResponse>()!;
        Assert.DoesNotContain(capabilities.Devices, d => d.IsPdfCapable);
        Assert.Empty(capabilities.Media);
    }

    [Fact]
    public async Task StbDrawingCapabilities_ReportsNamedStyleMode()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotCapabilitiesStb", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.GetPlotCapabilities, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        var capabilities = response.DeserializePayload<PlotCapabilitiesResponse>()!;
        Assert.Equal(CadPlotStyleMode.Named, capabilities.CurrentDrawingStyleMode);
    }
}
