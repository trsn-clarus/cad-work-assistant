using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
#if !NET8_0_OR_GREATER
using System.Security.AccessControl;
using System.Security.Principal;
#endif
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using Serilog;

namespace CADWorkAssistant.Infrastructure.Ipc;

/// <summary>
/// AutoCAD 프로토콜을 말하는 프로세스(실제 AutoCAD Plugin 또는 FakeAutoCAD) 하나를 위한 Named Pipe 서버.
/// AutoCAD 타입을 전혀 참조하지 않으므로 Infrastructure에 두고 두 프로세스가 그대로 재사용한다
/// (Milestone 2 §5 "Fake 전용 Protocol을 만들지 않는다"). 한 번에 클라이언트 하나를 받고, 연결이
/// 끊기면 다시 새 연결을 기다린다 (Desktop 재시작 후 재연결 지원, §28). Pipe는 현재 Windows 사용자만
/// 접근 가능하도록 제한한다 (§40).
/// </summary>
public sealed class AutoCadPipeServer
{
    private readonly IpcRequestDispatcher _dispatcher;
    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    public AutoCadPipeServer(IpcRequestDispatcher dispatcher, int autoCadProcessId)
    {
        _dispatcher = dispatcher;
        _pipeName = IpcProtocol.GetPipeName(autoCadProcessId);
    }

    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        Log.Information("CADWorkAssistant IPC pipe server listening on {PipeName}", _pipeName);
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 정상적인 종료 경로.
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "CADWorkAssistant IPC accept loop ended with an error during shutdown");
            }
        }

        _cts.Dispose();
        _cts = null;
        Log.Information("CADWorkAssistant IPC pipe server stopped");
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = CreatePipe(_pipeName);

                // WaitForConnectionAsync는 아직 아무도 연결하지 않은 상태에서 CancellationToken이
                // 취소돼도 실제로는 대기를 풀어주지 않는 경우가 있다(.NET의 알려진 동작 - 실제로 겪음).
                // Pipe 자체를 Dispose해서 강제로 대기를 깨운다.
                using (cancellationToken.Register(static state => ((NamedPipeServerStream)state!).Dispose(), pipe))
                {
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                }

                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 서버 종료 중 - 루프를 빠져나간다.
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // 위 Dispose 워크어라운드로 인한 정상 종료 경로.
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
                // 일부 런타임에서는 대기 중 Dispose가 IOException으로 나타난다 - 역시 정상 종료 경로.
            }
            catch (Exception ex)
            {
                // 클라이언트 한 명 처리 중 생긴 문제로 서버 전체가 죽으면 안 된다.
                Log.Warning(ex, "CADWorkAssistant IPC connection handling failed; waiting for next connection");
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? requestJson;
            try
            {
                requestJson = await PipeMessageFramer
                    .ReadMessageAsync(pipe, IpcProtocol.MaxMessageSizeBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException)
            {
                return; // 클라이언트가 비정상적으로 연결을 끊음.
            }

            if (requestJson is null)
            {
                return; // 클라이언트가 정상적으로 연결을 끊음.
            }

            await ProcessOneRequestAsync(pipe, requestJson, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneRequestAsync(NamedPipeServerStream pipe, string requestJson, CancellationToken cancellationToken)
    {
        IpcRequestEnvelope request;
        try
        {
            request = IpcRequestEnvelope.FromJson(requestJson);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Received malformed IPC request, dropping connection");
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCts.CancelAfter(IpcProtocol.RequestTimeoutMs);

        var response = await _dispatcher.DispatchAsync(request, requestCts.Token).ConfigureAwait(false);

        // DispatchAsync는 취소를 전부 "Cancelled"로 뭉뚱그린다. 서버 종료가 아니라 우리가 건
        // requestCts 타임아웃 때문이라면 사용자에게는 더 정확한 Timeout으로 알려준다.
        if (!response.Success
            && response.Error!.Code == IpcErrorCode.Cancelled
            && !cancellationToken.IsCancellationRequested)
        {
            response = IpcResponseEnvelope.Fail(
                request.RequestId,
                new IpcError(IpcErrorCode.Timeout, "AutoCAD did not respond in time."));
        }

        stopwatch.Stop();
        Log.Information(
            "IPC {MessageType} RequestId={RequestId} Success={Success} ErrorCode={ErrorCode} ElapsedMs={ElapsedMs}",
            request.MessageType,
            request.RequestId,
            response.Success,
            response.Error?.Code,
            stopwatch.ElapsedMilliseconds);

        try
        {
            await PipeMessageFramer.WriteMessageAsync(pipe, response.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // 응답을 쓰기 전에 클라이언트가 사라짐 - 다음 연결을 기다리면 된다.
        }
    }

    private static NamedPipeServerStream CreatePipe(string pipeName)
    {
#if NET8_0_OR_GREATER
        // CurrentUserOnly: OS가 현재 Windows 사용자로만 Pipe 접근을 제한해준다 - PipeSecurity를
        // 직접 조립할 필요가 없다 (.NET Core 전용 플래그, net48에는 없음).
        // 참고: 커스텀 PipeSecurity + NamedPipeServerStreamAcl.Create 조합은 실제로 시도했을 때
        // IOException("매개 변수가 틀렸습니다")을 냈다 - 그래서 더 단순한 이 방식을 쓴다 (§40).
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            inBufferSize: 65536,
            outBufferSize: 65536);
#else
        // .NET Framework 4.8에는 CurrentUserOnly가 없다 - PipeSecurity를 직접 구성한다.
        var pipeSecurity = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        }

        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 65536,
            outBufferSize: 65536,
            pipeSecurity);
#endif
    }
}
