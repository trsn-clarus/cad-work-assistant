using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// "선택 객체만 보기" (§32-34). ModelSpace 전체를 순회해 선택되지 않은, 그리고 지금 실제로 보이는
/// Entity만 숨기고 그 Handle을 기억한다 - RestoreVisibility는 정확히 이 목록만 다시 보이게 한다
/// (원래부터 안 보이던 객체까지 보이게 만들지 않는다, §46). Entity.Visible은 원본 도면을 영구
/// 변경하지 않는다 - AutoCAD가 Undo에 남기지 않는 표시 상태 플래그다(§104, 문서화 예정).
/// </summary>
internal sealed class IsolateObjectsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;
    private readonly DrawingIsolationState _isolationState;

    public IsolateObjectsHandler(IAutoCadDispatcher dispatcher, DrawingIsolationState isolationState)
    {
        _dispatcher = dispatcher;
        _isolationState = isolationState;
    }

    public string MessageType => IpcMessageTypes.IsolateObjects;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<IsolateObjectsRequest>(IpcJson.Options);
        if (request is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "IsolateObjects requires handles."));
        }

        var succeeded = await _dispatcher
            .InvokeAsync(() => Isolate(new System.Collections.Generic.HashSet<string>(request.Handles)), cancellationToken)
            .ConfigureAwait(false);

        return succeeded
            ? IpcHandlerResult.Ok(payload: null)
            : IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."));
    }

    private bool Isolate(System.Collections.Generic.HashSet<string> keepVisibleHandles)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return false;
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        using var transaction = database.TransactionManager.StartTransaction();

        var modelSpace = (BlockTableRecord)transaction.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        var newlyHidden = new System.Collections.Generic.HashSet<string>();

        foreach (var objectId in modelSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            var handle = entity.Handle.ToString();
            if (keepVisibleHandles.Contains(handle) || !entity.Visible)
            {
                continue;
            }

            entity.UpgradeOpen();
            entity.Visible = false;
            newlyHidden.Add(handle);
        }

        transaction.Commit();

        // 이미 활성 Isolation이 있으면(연속으로 다시 선택) 기존에 숨긴 목록에 이번 것도 합친다 -
        // Restore는 여러 번의 Isolate를 통틀어 CWA가 숨긴 전부를 되돌려야 한다.
        _isolationState.HiddenObjectHandles ??= new System.Collections.Generic.HashSet<string>();
        _isolationState.HiddenObjectHandles.UnionWith(newlyHidden);

        return true;
    }
}
