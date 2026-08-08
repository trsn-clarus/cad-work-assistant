using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// 사용자가 AutoCAD에서 Polyline/Circle/Ellipse/Region을 선택하면 원본 면적 데이터를 반환한다.
/// Read-only이며 절대 Commit하지 않는다. Length의 SelectLengthObjectsHandler와 동일한 패턴을
/// 그대로 따른다 (Milestone 3 §5, §37-40) - Editor.GetSelection은 사용자 입력을 기다리므로
/// InvokeInCommandContextAsync로 실행한다.
/// </summary>
internal sealed class SelectAreaObjectsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public SelectAreaObjectsHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.SelectAreaObjects;

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
    private static SelectionOutcome<AreaSelectionResponse> RunSelection()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return SelectionOutcome<AreaSelectionResponse>.NoActiveDocument();
        }

        using var documentLock = document.LockDocument();

        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\n면적을 계산할 닫힌 영역을 선택하세요: "
        };

        var selectionResult = document.Editor.GetSelection(options);

        if (selectionResult.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<AreaSelectionResponse>.Cancelled();
        }

        if (selectionResult.Status != PromptStatus.OK)
        {
            return SelectionOutcome<AreaSelectionResponse>.Error($"AutoCAD selection failed with status {selectionResult.Status}.");
        }

        var objects = new List<CadAreaObjectDto>();
        var excludedTypeNames = new List<string>();

        // Read-only: 절대 Commit하지 않는다 - using이 끝나면 자동으로 Abort된다 (§39).
        using var transaction = document.Database.TransactionManager.StartTransaction();

        foreach (var objectId in selectionResult.Value.GetObjectIds())
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            var geometryType = CadAreaGeometryMapper.ToSupportedAreaGeometryType(entity);
            if (geometryType is null)
            {
                excludedTypeNames.Add(entity.GetType().Name);
                continue;
            }

            var (isClosed, area) = ReadClosedAndArea(entity, geometryType.Value);
            objects.Add(new CadAreaObjectDto(entity.Handle.ToString(), geometryType.Value, entity.Layer, area, isClosed));
        }

        var unit = CadUnitMapper.ToDrawingUnit(document.Database.Insunits);
        var response = new AreaSelectionResponse(objects, excludedTypeNames.Distinct().ToList(), unit);

        return SelectionOutcome<AreaSelectionResponse>.Selected(response);
    }

    // Region은 정의상 항상 닫힌 면이라 Closed 개념이 없다 (§93-94에서 리플렉션으로 확인). Curve 계열
    // (Polyline/Circle/Ellipse)만 Closed를 실제로 검사한다. Area를 읽다가 AutoCAD가 예외를 던지면
    // (예: 자기교차 등 비정상 형상, §17-18) NaN을 돌려준다 - Core가 InvalidGeometry로 분류한다.
    private static (bool IsClosed, double Area) ReadClosedAndArea(Entity entity, SupportedAreaGeometryType geometryType)
    {
        if (geometryType == SupportedAreaGeometryType.Region)
        {
            var region = (Region)entity;
            try
            {
                return (true, region.Area);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return (true, double.NaN);
            }
        }

        var curve = (Curve)entity;
        if (!curve.Closed)
        {
            return (false, 0);
        }

        try
        {
            return (true, curve.Area);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return (true, double.NaN);
        }
    }
}
