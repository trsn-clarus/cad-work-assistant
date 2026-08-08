using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

internal sealed class GetDrawingContextHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public GetDrawingContextHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.GetDrawingContext;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var context = await _dispatcher.InvokeAsync(BuildContextOrNull, cancellationToken).ConfigureAwait(false);

        return context is null
            ? IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."))
            : IpcHandlerResult.Ok(context);
    }

    // AutoCAD Application Context 안에서만 실행된다 (AutoCadDispatcher를 통해 호출됨).
    private static DrawingContext? BuildContextOrNull()
    {
        var documents = Application.DocumentManager;
        var activeDocument = documents.MdiActiveDocument;

        if (documents.Count == 0 || activeDocument is null)
        {
            return null;
        }

        var database = activeDocument.Database;

        return new DrawingContext(
            documentDisplayName: Path.GetFileName(activeDocument.Name),
            fullPath: activeDocument.IsNamedDrawing ? activeDocument.Name : null,
            isSaved: activeDocument.IsNamedDrawing,
            isReadOnly: activeDocument.IsReadOnly,
            layout: LayoutManager.Current.CurrentLayout,
            units: CadUnitMapper.ToDrawingUnit(database.Insunits),
            documentCount: documents.Count);
    }
}
