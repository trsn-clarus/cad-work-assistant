using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using Serilog;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// 이 AutoCAD 프로세스 전용 Named Pipe 서버. 한 번에 클라이언트 하나를 받고, 연결이 끊기면
/// 다시 새 연결을 기다린다 (Desktop 재시작 후 재연결 지원, §28). Pipe는 현재 Windows 사용자만
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
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 서버 종료 중 - 루프를 빠져나간다.
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
    }
}
