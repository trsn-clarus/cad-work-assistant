using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fakes;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Length;

/// <summary>
/// Milestone 2 §52: Selection Cancel / Timeout / Pipe Disconnect / AutoCAD Error 전부 Desktop
/// (여기서는 AutoCadPipeClient)이 죽지 않고 구조화된 결과로 받아야 한다.
/// </summary>
public class LengthSelectionFailureTests
{
    [Fact]
    public async Task SelectionCancelled_ReturnsStructuredCancelNotAnException()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("SelectionCancelled", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.SelectionCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task AutoCadError_ReturnsStructuredErrorWithoutLeakingInternals()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("AutoCadError", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.ApiExecutionFailed, response.Error!.Code);
    }

    [Fact]
    public async Task RequestTimeout_ClientFailsSafelyInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("RequestTimeout", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        // 서버가 절대 응답하지 않는 시나리오 - 짧은 요청 타임아웃으로 클라이언트가 스스로 포기해야 한다.
        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, requestTimeoutMs: 500, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectionLost_ClientObservesFailureInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("ConnectionLost", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        // 이 시나리오의 FakeAutoCad는 응답 직전 프로세스 자체를 종료한다 (AutoCAD 크래시 흉내).
        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None));
    }

    [Fact]
    public async Task ServerFaultsMidRequest_ClientTimesOutInsteadOfHangingForever()
    {
        // 실제 JSON framing/truncation 관련 malformed-response 케이스는 Core.Tests의
        // PipeMessageFramerTests(MemoryStream으로 바이트를 직접 제어)에서 이미 다뤘다. 여기서는
        // "서버가 요청 처리 도중 처리되지 않은 예외로 응답을 아예 안 보내는" 경우 - 클라이언트가
        // 영원히 기다리지 않고 자기 request timeout으로 안전하게 빠져나오는지를 실제 Pipe로 확인한다.
        var processId = Random.Shared.Next(90_000, 99_999);
        await using var server = new FakeAutoCadServer(processId, (_, _) =>
            throw new InvalidOperationException("simulated unhandled server fault - response is never sent"));
        server.Start();

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(processId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, requestTimeoutMs: 800, CancellationToken.None));
    }
}
