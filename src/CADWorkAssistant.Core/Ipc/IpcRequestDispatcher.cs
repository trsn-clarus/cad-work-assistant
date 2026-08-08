using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CADWorkAssistant.Core.Ipc;

/// <summary>
/// MessageType별 <see cref="IIpcRequestHandler"/>로 요청을 라우팅한다. AutoCAD API를 전혀 모르므로
/// AutoCAD 없이 단위 테스트할 수 있다 (Fake handler 사용, §41). 실제 AutoCAD 호출은 각 Handler
/// 내부에서 AutoCAD Dispatcher를 통해 이뤄진다 - 여기서는 라우팅/버전 검증/오류 변환만 담당한다.
/// </summary>
public sealed class IpcRequestDispatcher
{
    private readonly Dictionary<string, IIpcRequestHandler> _handlers;

    public IpcRequestDispatcher(IEnumerable<IIpcRequestHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.MessageType, StringComparer.Ordinal);
    }

    public async Task<IpcResponseEnvelope> DispatchAsync(IpcRequestEnvelope request, CancellationToken cancellationToken)
    {
        if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            return IpcResponseEnvelope.Fail(
                request.RequestId,
                new IpcError(
                    IpcErrorCode.UnsupportedProtocol,
                    $"Plugin protocol {IpcProtocol.CurrentVersion}, request protocol {request.ProtocolVersion}."));
        }

        if (!_handlers.TryGetValue(request.MessageType, out var handler))
        {
            return IpcResponseEnvelope.Fail(
                request.RequestId,
                new IpcError(IpcErrorCode.InvalidRequest, $"Unknown message type '{request.MessageType}'."));
        }

        try
        {
            var result = await handler.HandleAsync(request.Payload, cancellationToken).ConfigureAwait(false);

            return result.Success
                ? IpcResponseEnvelope.Ok(request.RequestId, result.Payload)
                : IpcResponseEnvelope.Fail(request.RequestId, result.Error!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return IpcResponseEnvelope.Fail(request.RequestId, new IpcError(IpcErrorCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception ex)
        {
            return IpcResponseEnvelope.Fail(
                request.RequestId,
                new IpcError(IpcErrorCode.InternalError, "An unexpected error occurred while handling the request.", ex.ToString()));
        }
    }
}
