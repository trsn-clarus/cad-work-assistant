using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// Window/Crossing 영역으로 객체를 선택한다 (§26-30). Length/Area와 달리 Editor.GetSelection이
/// 아니라 "첫 번째 모서리 → 반대쪽 모서리"를 직접 GetPoint/GetCorner로 받은 뒤 SelectWindow/
/// SelectCrossingWindow에 넘긴다 - AutoCAD 사용자에게 익숙한 고무줄 사각형 UX를 그대로 재현한다
/// (§30, 실제 acmgd.dll에서 GetCorner(message, basePoint)/SelectWindow/SelectCrossingWindow
/// 존재를 리플렉션으로 확인함). 인터랙티브라 InvokeInCommandContextAsync로 실행한다.
/// </summary>
internal sealed class SelectDrawingObjectsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public SelectDrawingObjectsHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.SelectDrawingObjects;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<SelectDrawingObjectsRequest>(IpcJson.Options);
        if (request is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "SelectDrawingObjects requires a selection mode."));
        }

        var outcome = await _dispatcher
            .InvokeInCommandContextAsync(() => RunSelection(request.Mode), cancellationToken)
            .ConfigureAwait(false);

        return outcome.Kind switch
        {
            SelectionOutcomeKind.Selected => IpcHandlerResult.Ok(outcome.Response),
            SelectionOutcomeKind.Cancelled => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.SelectionCancelled, "Selection was cancelled.")),
            SelectionOutcomeKind.NoActiveDocument => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            _ => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.ApiExecutionFailed, outcome.ErrorMessage ?? "Selection failed."))
        };
    }

    // AutoCAD Command Context 안에서만 실행된다.
    // Autodesk.AutoCAD.EditorInput에도 같은 이름의 SelectionMode가 있어(Window/Crossing 의미가 다름)
    // 완전한 이름으로 구분한다.
    private static SelectionOutcome<DrawingSelectionResponse> RunSelection(CADWorkAssistant.Core.Drawing.SelectionMode mode)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return SelectionOutcome<DrawingSelectionResponse>.NoActiveDocument();
        }

        using var documentLock = document.LockDocument();
        var editor = document.Editor;

        var firstCorner = editor.GetPoint("\n첫 번째 모서리를 지정하세요: ");
        if (firstCorner.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<DrawingSelectionResponse>.Cancelled();
        }

        if (firstCorner.Status != PromptStatus.OK)
        {
            return SelectionOutcome<DrawingSelectionResponse>.Error($"AutoCAD point input failed with status {firstCorner.Status}.");
        }

        var secondCorner = editor.GetCorner("\n반대쪽 모서리를 지정하세요: ", firstCorner.Value);
        if (secondCorner.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<DrawingSelectionResponse>.Cancelled();
        }

        if (secondCorner.Status != PromptStatus.OK)
        {
            return SelectionOutcome<DrawingSelectionResponse>.Error($"AutoCAD point input failed with status {secondCorner.Status}.");
        }

        var selectionResult = mode == CADWorkAssistant.Core.Drawing.SelectionMode.Window
            ? editor.SelectWindow(firstCorner.Value, secondCorner.Value)
            : editor.SelectCrossingWindow(firstCorner.Value, secondCorner.Value);

        if (selectionResult.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<DrawingSelectionResponse>.Cancelled();
        }

        if (selectionResult.Status != PromptStatus.OK)
        {
            // 영역 안/걸침에 아무 객체도 없으면 OK가 아니라 Error 상태로 오는 AutoCAD 버전이 있다 -
            // 이건 진짜 오류가 아니라 "빈 선택"이므로 빈 목록으로 정상 응답한다.
            return SelectionOutcome<DrawingSelectionResponse>.Selected(new DrawingSelectionResponse(new List<CadSelectedObjectDto>()));
        }

        var objects = new List<CadSelectedObjectDto>();

        // Read-only: 절대 Commit하지 않는다 (§31).
        using var transaction = document.Database.TransactionManager.StartTransaction();

        foreach (var objectId in selectionResult.Value.GetObjectIds())
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            objects.Add(new CadSelectedObjectDto(
                entity.Handle.ToString(),
                entity.GetType().Name,
                entity.Layer,
                GetDrawingOverviewHandler.TryGetBounds(entity)));
        }

        return SelectionOutcome<DrawingSelectionResponse>.Selected(new DrawingSelectionResponse(objects));
    }
}
