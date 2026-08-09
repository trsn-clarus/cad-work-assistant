using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// 사용자가 AutoCAD에서 선택한 객체 중 DBText/MText만 반환한다 (Milestone 12 §7-8, §17). Length/Area의
/// SelectLengthObjects/SelectAreaObjects와 완전히 같은 Editor.GetSelection 패턴이다 - Window/Crossing
/// 두 점을 직접 받는 SelectDrawingObjects(Milestone 5)와는 다르다(§14의 "AutoCAD에서 사용자가
/// Text/MText 선택"은 AutoCAD 자체의 표준 선택 UX를 그대로 쓴다는 뜻이다). Dimension/MLeader/Table/
/// AttributeReference 등은 문자 유사 내용이 있어도 제외 목록으로 돌아온다(§8).
/// </summary>
internal sealed class SelectTextObjectsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public SelectTextObjectsHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.SelectTextObjects;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var outcome = await _dispatcher.InvokeInCommandContextAsync(RunSelection, cancellationToken).ConfigureAwait(false);

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
    private static SelectionOutcome<TextSelectionResponse> RunSelection()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return SelectionOutcome<TextSelectionResponse>.NoActiveDocument();
        }

        using var documentLock = document.LockDocument();

        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\n편집할 문자 객체를 선택하세요: "
        };

        var selectionResult = document.Editor.GetSelection(options);

        if (selectionResult.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<TextSelectionResponse>.Cancelled();
        }

        if (selectionResult.Status != PromptStatus.OK)
        {
            return SelectionOutcome<TextSelectionResponse>.Error($"AutoCAD selection failed with status {selectionResult.Status}.");
        }

        var objects = new List<CadTextObjectDto>();
        var excludedTypeNames = new List<string>();

        // Read-only: 절대 Commit하지 않는다(§61 원칙과 동일).
        using var transaction = document.Database.TransactionManager.StartTransaction();

        foreach (var objectId in selectionResult.Value.GetObjectIds())
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            if (!AutoCadTextEntityAdapter.IsSupported(entity))
            {
                excludedTypeNames.Add(entity.GetType().Name);
                continue;
            }

            objects.Add(AutoCadTextEntityAdapter.BuildDto(entity, transaction));
        }

        var response = new TextSelectionResponse(objects, excludedTypeNames.Distinct().ToList());
        return SelectionOutcome<TextSelectionResponse>.Selected(response);
    }
}
