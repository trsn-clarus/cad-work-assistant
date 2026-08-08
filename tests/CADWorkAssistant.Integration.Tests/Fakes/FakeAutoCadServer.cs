using System.IO.Pipes;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;

namespace CADWorkAssistant.Integration.Tests.Fakes;

/// <summary>
/// 실제 AutoCAD 없이 Desktop 쪽 IPC 클라이언트(Infrastructure.Ipc.AutoCadPipeClient)를 종단간으로
/// 검증하기 위한 테스트 전용 Named Pipe 서버 (Milestone 1 §42). Production 코드에는 포함하지 않는다.
/// </summary>
internal sealed class FakeAutoCadServer : IAsyncDisposable
{
    private readonly int _processId;
    private readonly Func<IpcRequestEnvelope, CancellationToken, Task<IpcResponseEnvelope>> _responder;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    public FakeAutoCadServer(int processId, Func<IpcRequestEnvelope, CancellationToken, Task<IpcResponseEnvelope>> responder)
    {
        _processId = processId;
        _responder = responder;
    }

    public void Start()
    {
        _loopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var pipeName = IpcProtocol.GetPipeName(_processId);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var requestJson = await PipeMessageFramer
                        .ReadMessageAsync(pipe, IpcProtocol.MaxMessageSizeBytes, cancellationToken)
                        .ConfigureAwait(false);

                    if (requestJson is null)
                    {
                        break;
                    }

                    var request = IpcRequestEnvelope.FromJson(requestJson);
                    var response = await _responder(request, cancellationToken).ConfigureAwait(false);
                    await PipeMessageFramer.WriteMessageAsync(pipe, response.ToJson(), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // 클라이언트가 갑자기 끊었다 - 다음 연결을 기다린다.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }
}
