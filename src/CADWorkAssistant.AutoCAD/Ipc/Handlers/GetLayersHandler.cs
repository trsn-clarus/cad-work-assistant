using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>도면의 전체 Layer 목록과 상태를 반환한다 (§38-39). Read-only.</summary>
internal sealed class GetLayersHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public GetLayersHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.GetLayers;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var response = await _dispatcher.InvokeAsync(BuildResponseOrNull, cancellationToken).ConfigureAwait(false);

        return response is null
            ? IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."))
            : IpcHandlerResult.Ok(response);
    }

    private static GetLayersResponse? BuildResponseOrNull()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return null;
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        using var transaction = database.TransactionManager.StartTransaction();

        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        var layers = new List<CadLayerDto>();

        foreach (var layerId in layerTable)
        {
            var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForRead);
            layers.Add(new CadLayerDto(
                layer.Name,
                isOn: !layer.IsOff,
                isFrozen: layer.IsFrozen,
                isLocked: layer.IsLocked,
                isPlottable: layer.IsPlottable,
                colorIndex: layer.EntityColor.ColorIndex,
                isCurrent: layerId == database.Clayer));
        }

        return new GetLayersResponse(layers);
    }
}
