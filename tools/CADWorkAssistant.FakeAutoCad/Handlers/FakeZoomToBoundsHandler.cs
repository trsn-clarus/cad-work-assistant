using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>실제 View 조작은 없다 - 요청 payload가 올바르게 역직렬화되는지만 검증한다(§23).</summary>
internal sealed class FakeZoomToBoundsHandler : IIpcRequestHandler
{
    public string MessageType => IpcMessageTypes.ZoomToBounds;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<ZoomToBoundsRequest>(IpcJson.Options);
        return Task.FromResult(request is null
            ? IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "ZoomToBounds requires bounds."))
            : IpcHandlerResult.Ok(payload: null));
    }
}
