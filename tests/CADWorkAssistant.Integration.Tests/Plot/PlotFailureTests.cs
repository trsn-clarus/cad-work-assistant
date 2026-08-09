using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Plot;

/// <summary>Milestone 11 §107 - Busy/Failure/Disconnect/Window 취소가 전부 구조화된 결과로 와야
/// 한다 - DrawingSelectionFailureTests와 같은 패턴.</summary>
public class PlotFailureTests
{
    [Fact]
    public async Task WindowCancelled_ReturnsStructuredCancelNotAnException()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotWindowCancelled", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.AcquirePlotWindow, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.SelectionCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task PlotBusy_ReturnsApiExecutionFailedWithoutLeakingInternals()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotBusy", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.PlotDrawingPdf,
            BuildRequest(Path.Combine(Path.GetTempPath(), "should-not-be-created-busy.pdf")),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.ApiExecutionFailed, response.Error!.Code);
    }

    [Fact]
    public async Task PlotFailure_ReturnsApiExecutionFailed()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotFailure", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.PlotDrawingPdf,
            BuildRequest(Path.Combine(Path.GetTempPath(), "should-not-be-created-failure.pdf")),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.ApiExecutionFailed, response.Error!.Code);
    }

    [Fact]
    public async Task PlotDisconnect_ClientObservesFailureInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotDisconnect", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.PlotDrawingPdf,
            BuildRequest(Path.Combine(Path.GetTempPath(), "should-not-be-created-disconnect.pdf")),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None));
    }

    [Fact]
    public async Task PlotDrawingPdf_MissingTargetPath_FailsWithInvalidRequest()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("PlotSuccess", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.PlotDrawingPdf,
            new PlotDrawingPdfRequest(
                CadPlotScope.CurrentLayout, layoutName: null, window: null,
                CadPaperSizeCatalog.A4.Name, CadPlotOrientation.Auto, CadPlotColorMode.KeepExisting, targetFilePath: ""),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    private static PlotDrawingPdfRequest BuildRequest(string targetPath) => new(
        CadPlotScope.CurrentLayout, layoutName: null, window: null,
        CadPaperSizeCatalog.A4.Name, CadPlotOrientation.Auto, CadPlotColorMode.KeepExisting, targetPath);
}
