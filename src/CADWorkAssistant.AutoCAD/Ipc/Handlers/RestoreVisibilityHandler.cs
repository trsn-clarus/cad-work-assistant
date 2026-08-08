using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// IsolateObjects/SetLayerVisibility가 남긴 <see cref="DrawingIsolationState"/> 스냅샷을 정확히
/// 되돌린다 (§35, §45-46). "복원 = 전부 On" 이 아니라 "복원 = 변경 직전 상태" - 원래 꺼져 있던
/// Layer는 복원 후에도 꺼진 채로 남는다. 되돌릴 활성 Isolation이 없으면 그냥 성공으로 끝난다
/// (§98 - 아무것도 안 바뀐 상태에서 눌러도 오류가 아니다).
/// </summary>
internal sealed class RestoreVisibilityHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;
    private readonly DrawingIsolationState _isolationState;

    public RestoreVisibilityHandler(IAutoCadDispatcher dispatcher, DrawingIsolationState isolationState)
    {
        _dispatcher = dispatcher;
        _isolationState = isolationState;
    }

    public string MessageType => IpcMessageTypes.RestoreVisibility;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var succeeded = await _dispatcher.InvokeAsync(Restore, cancellationToken).ConfigureAwait(false);

        return succeeded
            ? IpcHandlerResult.Ok(payload: null)
            : IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."));
    }

    private bool Restore()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return false;
        }

        if (!_isolationState.HasActiveIsolation)
        {
            return true;
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        using var transaction = database.TransactionManager.StartTransaction();

        if (_isolationState.HiddenObjectHandles is { } hiddenHandles)
        {
            foreach (var handleText in hiddenHandles)
            {
                if (!TryGetEntityForWrite(database, transaction, handleText, out var entity))
                {
                    continue; // §48: Isolation 중 사용자가 직접 지웠을 수도 있다 - 조용히 건너뛴다.
                }

                entity.Visible = true;
            }
        }

        if (_isolationState.OriginalLayerOnState is { } originalLayerStates)
        {
            var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);

            // net48은 KeyValuePair<,>.Deconstruct가 없어(netstandard2.1+ 전용) .Key/.Value로 직접 접근한다.
            foreach (var entry in originalLayerStates)
            {
                var layerName = entry.Key;
                var wasOn = entry.Value;

                if (!layerTable.Has(layerName))
                {
                    continue; // §48: Layer가 그 사이 삭제/이름변경 됐을 수 있다.
                }

                var layer = (LayerTableRecord)transaction.GetObject(layerTable[layerName], OpenMode.ForRead);
                if (layer.IsOff == !wasOn)
                {
                    continue;
                }

                layer.UpgradeOpen();
                layer.IsOff = !wasOn;
            }
        }

        transaction.Commit();
        _isolationState.Clear();
        return true;
    }

    private static bool TryGetEntityForWrite(Database database, Transaction transaction, string handleText, out Entity entity)
    {
        entity = null!;

        if (!long.TryParse(handleText, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var handleValue))
        {
            return false;
        }

        if (!database.TryGetObjectId(new Handle(handleValue), out var objectId) || objectId.IsErased)
        {
            return false;
        }

        if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity found)
        {
            return false;
        }

        found.UpgradeOpen();
        entity = found;
        return true;
    }
}
