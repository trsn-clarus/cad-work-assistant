using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>"선택 영역 보기" 등 - Desktop이 이미 들고 있는 Bounds(예: SelectionSession.Bounds)로
/// View를 맞춘다 (§23).</summary>
internal sealed class ZoomToBoundsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public ZoomToBoundsHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.ZoomToBounds;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<ZoomToBoundsRequest>(IpcJson.Options);
        if (request is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "ZoomToBounds requires bounds."));
        }

        var succeeded = await _dispatcher.InvokeAsync(() => Zoom(request.Bounds), cancellationToken).ConfigureAwait(false);

        return succeeded
            ? IpcHandlerResult.Ok(payload: null)
            : IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."));
    }

    private static bool Zoom(CadBoundsDto bounds)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return false;
        }

        using var documentLock = document.LockDocument();
        DrawingZoomService.ZoomToBounds(document, bounds);
        return true;
    }
}
