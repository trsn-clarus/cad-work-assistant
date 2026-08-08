using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fakes;

namespace CADWorkAssistant.Integration.Tests.Ipc;

/// <summary>
/// 실제 AutoCAD 없이, Desktop이 실제로 쓰는 AutoCadPipeClient를 실제 Named Pipe로 검증한다
/// (Fake 서버 상대, §42). AutoCAD 자체(Dispatcher/Handlers)는 이 PC에 설치된 AutoCAD 2024
/// Managed API 참조로 컴파일 검증까지 마쳤고, 실제 GUI 연동 스모크 테스트는 AutoCAD가 정상
/// 동작하는 머신에서 별도로 수행한다 (docs/ROADMAP.md 참고).
/// </summary>
public class AutoCadPipeClientTests
{
    // 병렬로 실행되는 다른 테스트와 Pipe 이름이 겹치지 않도록 매번 임의의 PID를 사용한다.
    private static int NextProcessId() => Random.Shared.Next(90_000, 99_999);

    [Fact]
    public async Task ConnectAndSend_RoundTripsThroughRealNamedPipe()
    {
        var processId = NextProcessId();
        await using var server = new FakeAutoCadServer(processId, (request, _) =>
            Task.FromResult(IpcResponseEnvelope.Ok(request.RequestId, new { pong = true })));
        server.Start();

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(processId, connectTimeoutMs: 2000, CancellationToken.None);

        var response = await client.SendRequestAsync(IpcMessageTypes.Ping, null, requestTimeoutMs: 2000, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_Throws_WhenNoServerIsListening()
    {
        var processId = NextProcessId();
        using var client = new AutoCadPipeClient();

        await Assert.ThrowsAnyAsync<Exception>(
            () => client.ConnectAsync(processId, connectTimeoutMs: 300, CancellationToken.None));
    }

    [Fact]
    public async Task SendRequestAsync_PreservesRequestIdAcrossTheWire()
    {
        var processId = NextProcessId();
        IpcRequestEnvelope? seenRequest = null;

        await using var server = new FakeAutoCadServer(processId, (request, _) =>
        {
            seenRequest = request;
            return Task.FromResult(IpcResponseEnvelope.Ok(request.RequestId, new { echoed = true }));
        });
        server.Start();

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(processId, 2000, CancellationToken.None);

        var response = await client.SendRequestAsync(IpcMessageTypes.GetApplicationInfo, null, 2000, CancellationToken.None);

        Assert.NotNull(seenRequest);
        Assert.Equal(seenRequest!.RequestId, response.RequestId);
    }

    [Fact]
    public async Task SendRequestAsync_ErrorResponse_PropagatesErrorCode()
    {
        var processId = NextProcessId();
        await using var server = new FakeAutoCadServer(processId, (request, _) =>
            Task.FromResult(IpcResponseEnvelope.Fail(
                request.RequestId,
                new IpcError(IpcErrorCode.NoActiveDocument, "No document is open."))));
        server.Start();

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(processId, 2000, CancellationToken.None);

        var response = await client.SendRequestAsync(IpcMessageTypes.GetDrawingContext, null, 2000, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.NoActiveDocument, response.Error!.Code);
    }

    [Fact]
    public async Task SendRequestAsync_TimesOut_WhenServerNeverResponds()
    {
        var processId = NextProcessId();
        await using var server = new FakeAutoCadServer(processId, async (request, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return IpcResponseEnvelope.Ok(request.RequestId, null);
        });
        server.Start();

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(processId, 2000, CancellationToken.None);

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.SendRequestAsync(IpcMessageTypes.Ping, null, requestTimeoutMs: 300, CancellationToken.None));
    }

    [Fact]
    public async Task SendRequestAsync_MultipleSequentialRequests_AllSucceed()
    {
        var processId = NextProcessId();
        var callCount = 0;

        await using var server = new FakeAutoCadServer(processId, (request, _) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult(IpcResponseEnvelope.Ok(request.RequestId, new { call = callCount }));
        });
        server.Start();

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(processId, 2000, CancellationToken.None);

        for (var i = 0; i < 5; i++)
        {
            var response = await client.SendRequestAsync(IpcMessageTypes.Ping, null, 2000, CancellationToken.None);
            Assert.True(response.Success);
        }

        Assert.Equal(5, callCount);
    }
}
