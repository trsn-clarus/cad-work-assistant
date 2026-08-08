using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// 지정한 Layer들의 On/Off를 바꾼다 - 개별 토글과 "선택 Layer만 보기" 둘 다 이 하나의 요청으로
/// 처리한다 (§37, §41). 이번 변경으로 처음 활성화되는 Isolation이면(§45) 적용 전 전체 Layer 상태를
/// 스냅샷으로 남긴다 - 이미 스냅샷이 있으면(연속 토글) 원래 스냅샷을 덮어쓰지 않는다. 현재 Layer는
/// Off 대상에서 제외한다(§44).
/// </summary>
internal sealed class SetLayerVisibilityHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;
    private readonly DrawingIsolationState _isolationState;

    public SetLayerVisibilityHandler(IAutoCadDispatcher dispatcher, DrawingIsolationState isolationState)
    {
        _dispatcher = dispatcher;
        _isolationState = isolationState;
    }

    public string MessageType => IpcMessageTypes.SetLayerVisibility;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<SetLayerVisibilityRequest>(IpcJson.Options);
        if (request is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "SetLayerVisibility requires at least one change."));
        }

        var succeeded = await _dispatcher.InvokeAsync(() => Apply(request.Changes), cancellationToken).ConfigureAwait(false);

        return succeeded
            ? IpcHandlerResult.Ok(payload: null)
            : IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."));
    }

    private bool Apply(IReadOnlyList<LayerVisibilityChange> changes)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return false;
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        using var transaction = database.TransactionManager.StartTransaction();
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);

        if (_isolationState.OriginalLayerOnState is null)
        {
            var snapshot = new Dictionary<string, bool>();
            foreach (var layerId in layerTable)
            {
                var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForRead);
                snapshot[layer.Name] = !layer.IsOff;
            }

            _isolationState.OriginalLayerOnState = snapshot;
        }

        foreach (var change in changes)
        {
            if (!layerTable.Has(change.LayerName))
            {
                continue; // §48: 존재하지 않는 Layer 요청은 조용히 무시한다.
            }

            var layerId = layerTable[change.LayerName];

            // §44: 현재 활성 Layer는 꺼서 작업을 혼란스럽게 만들지 않는다 - Off 요청만 건너뛰고
            // On 요청은 그대로 적용한다(이미 켜져 있어 사실상 no-op).
            if (!change.IsOn && layerId == database.Clayer)
            {
                continue;
            }

            var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForRead);
            if (layer.IsOff == change.IsOn)
            {
                layer.UpgradeOpen();
                layer.IsOff = !change.IsOn;
            }
        }

        transaction.Commit();
        return true;
    }
}
