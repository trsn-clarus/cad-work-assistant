using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// 사용자가 AutoCAD에서 Line/Polyline/Arc를 선택하면 원본 길이 데이터를 반환한다. Read-only이며
/// (§61) 절대 Commit하지 않는다. Editor.GetSelection은 사용자 입력을 기다리므로 반드시
/// InvokeInCommandContextAsync로 실행한다 (§15, §17).
/// </summary>
internal sealed class SelectLengthObjectsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public SelectLengthObjectsHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.SelectLengthObjects;

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

    // AutoCAD Command Context 안에서만 실행된다 (AutoCadDispatcher를 통해 호출됨). Editor.GetSelection이
    // 사용자가 선택을 마칠 때까지 여기서 블로킹되는 것은 의도된 동작이다.
    private static SelectionOutcome RunSelection()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return SelectionOutcome.NoActiveDocument();
        }

        using var documentLock = document.LockDocument();

        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\n길이를 계산할 객체를 선택하세요: "
        };

        var selectionResult = document.Editor.GetSelection(options);

        if (selectionResult.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome.Cancelled();
        }

        if (selectionResult.Status != PromptStatus.OK)
        {
            return SelectionOutcome.Error($"AutoCAD selection failed with status {selectionResult.Status}.");
        }

        var objects = new List<CadLengthObjectDto>();
        var excludedTypeNames = new List<string>();

        // Read-only: 절대 Commit하지 않는다 (§61) - using이 끝나면 자동으로 Abort된다.
        using var transaction = document.Database.TransactionManager.StartTransaction();

        foreach (var objectId in selectionResult.Value.GetObjectIds())
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            var geometryType = CadGeometryMapper.ToSupportedGeometryType(entity);
            if (geometryType is null)
            {
                excludedTypeNames.Add(entity.GetType().Name);
                continue;
            }

            var curve = (Curve)entity;
            var length = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);

            objects.Add(new CadLengthObjectDto(entity.Handle.ToString(), geometryType.Value, entity.Layer, length));
        }

        var unit = CadUnitMapper.ToDrawingUnit(document.Database.Insunits);
        var response = new LengthSelectionResponse(objects, excludedTypeNames.Distinct().ToList(), unit);

        return SelectionOutcome.Selected(response);
    }
}
