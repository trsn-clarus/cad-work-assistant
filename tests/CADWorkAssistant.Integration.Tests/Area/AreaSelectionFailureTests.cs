using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Area;

/// <summary>
/// Length의 LengthSelectionFailureTests와 동일한 구조 (§5, §75): Cancel / Timeout / Pipe Disconnect /
/// AutoCAD Error 전부 Desktop(여기서는 AutoCadPipeClient)이 죽지 않고 구조화된 결과로 받아야 한다.
/// </summary>
public class AreaSelectionFailureTests
{
    [Fact]
    public async Task SelectionCancelled_ReturnsStructuredCancelNotAnException()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("AreaSelectionCancelled", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.SelectionCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task AutoCadError_ReturnsStructuredErrorWithoutLeakingInternals()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("AreaAutoCadError", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.ApiExecutionFailed, response.Error!.Code);
    }

    [Fact]
    public async Task RequestTimeout_ClientFailsSafelyInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("AreaRequestTimeout", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, requestTimeoutMs: 500, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectionLost_ClientObservesFailureInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("AreaConnectionLost", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None));
    }
}
