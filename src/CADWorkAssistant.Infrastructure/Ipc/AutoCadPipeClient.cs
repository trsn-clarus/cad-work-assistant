using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.Infrastructure.Ipc;

/// <summary>
/// AutoCAD Plugin의 Named Pipe Server에 붙는 클라이언트 쪽 transport. Desktop이 사용하지만,
/// WPF에 의존하지 않으므로 Integration.Tests에서 Fake Server를 상대로도 그대로 쓸 수 있다 (§42).
/// 한 번에 하나의 요청/응답만 처리한다 (Semaphore로 직렬화) - Milestone 1 트래픽에는 충분하다.
/// </summary>
public sealed class AutoCadPipeClient : IDisposable
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private NamedPipeClientStream? _pipe;

    public bool IsConnected => _pipe is { IsConnected: true };

    /// <summary>주어진 PID의 AutoCAD Pipe에 연결을 시도한다. 실패하면 예외를 던진다 (Pipe가 없거나 Timeout).</summary>
    public async Task ConnectAsync(int autoCadProcessId, int connectTimeoutMs, CancellationToken cancellationToken)
    {
        var pipeName = IpcProtocol.GetPipeName(autoCadProcessId);
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            // NamedPipeClientStream.Connect(int)는 모든 대상 TFM에서 쓸 수 있는 가장 안전한 오버로드다.
            // Pipe가 아직 없으면 timeout까지 내부적으로 재시도하다가 TimeoutException을 던진다.
            await Task.Run(() => pipe.Connect(connectTimeoutMs), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            pipe.Dispose();
            throw;
        }

        _pipe?.Dispose();
        _pipe = pipe;
    }

    public async Task<IpcResponseEnvelope> SendRequestAsync(
        string messageType,
        object? payload,
        int requestTimeoutMs,
        CancellationToken cancellationToken)
    {
        if (_pipe is not { IsConnected: true } pipe)
        {
            throw new InvalidOperationException("AutoCadPipeClient is not connected.");
        }

        var request = IpcRequestEnvelope.Create(messageType, payload);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(requestTimeoutMs);

            try
            {
                await PipeMessageFramer.WriteMessageAsync(pipe, request.ToJson(), timeoutCts.Token).ConfigureAwait(false);
                var responseJson = await PipeMessageFramer
                    .ReadMessageAsync(pipe, IpcProtocol.MaxMessageSizeBytes, timeoutCts.Token)
                    .ConfigureAwait(false);

                if (responseJson is null)
                {
                    throw new IOException("AutoCAD plugin closed the connection before responding.");
                }

                return IpcResponseEnvelope.FromJson(responseJson);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"IPC request '{messageType}' timed out after {requestTimeoutMs}ms.");
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
    }

    public void Dispose()
    {
        Disconnect();
        _mutex.Dispose();
    }
}
