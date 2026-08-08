using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>"전체 도면 보기" (§21). 도면 Extents는 GetDrawingOverviewHandler와 같은 방식(전체 순회)으로
/// 다시 계산한다 - Database.Extmin/Extmax는 마지막 Regen 시점 값이라 신뢰하지 않는다.</summary>
internal sealed class ZoomExtentsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public ZoomExtentsHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.ZoomExtents;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.InvokeAsync(Zoom, cancellationToken).ConfigureAwait(false);

        return result switch
        {
            ZoomResult.Success => IpcHandlerResult.Ok(payload: null),
            ZoomResult.NoActiveDocument => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            ZoomResult.EmptyDrawing => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.ApiExecutionFailed, "Drawing has no objects to zoom to.")),
            _ => IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, "Zoom failed."))
        };
    }

    private static ZoomResult Zoom()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return ZoomResult.NoActiveDocument;
        }

        using var documentLock = document.LockDocument();
        var overview = GetDrawingOverviewHandler.BuildOverviewForZoom(document);

        if (overview?.Extents is null)
        {
            return ZoomResult.EmptyDrawing;
        }

        DrawingZoomService.ZoomToExtents(document, overview.Extents);
        return ZoomResult.Success;
    }

    private enum ZoomResult
    {
        Success,
        NoActiveDocument,
        EmptyDrawing
    }
}
