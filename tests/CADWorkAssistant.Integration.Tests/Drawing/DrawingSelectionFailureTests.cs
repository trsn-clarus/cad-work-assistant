using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Drawing;

/// <summary>Milestone 5 §110: Cancel/Timeout/Disconnect/Error 전부 구조화된 결과로 와야 한다 -
/// Length/Area의 LengthSelectionFailureTests와 같은 패턴.</summary>
public class DrawingSelectionFailureTests
{
    [Fact]
    public async Task SelectionCancelled_ReturnsStructuredCancelNotAnException()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingSelectionCancelled", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectDrawingObjects,
            new SelectDrawingObjectsRequest(SelectionMode.Window),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.SelectionCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task AutoCadError_ReturnsStructuredErrorWithoutLeakingInternals()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingAutoCadError", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectDrawingObjects,
            new SelectDrawingObjectsRequest(SelectionMode.Crossing),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.ApiExecutionFailed, response.Error!.Code);
    }

    [Fact]
    public async Task RequestTimeout_ClientFailsSafelyInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingRequestTimeout", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectDrawingObjects,
            new SelectDrawingObjectsRequest(SelectionMode.Window),
            requestTimeoutMs: 500,
            CancellationToken.None));
    }

    [Fact]
    public async Task ConnectionLost_ClientObservesFailureInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingConnectionLost", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectDrawingObjects,
            new SelectDrawingObjectsRequest(SelectionMode.Window),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None));
    }
}
