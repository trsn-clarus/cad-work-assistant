using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// Milestone 11 §13, §54 - Model Space에서 사용자가 두 모서리를 지정해 Plot Window를 만든다.
/// SelectDrawingObjectsHandler와 완전히 같은 GetPoint→GetCorner UX를 재사용한다(§30 원칙과 동일하게
/// 임의로 새 상호작용 패턴을 만들지 않는다) - 다만 결과가 선택된 객체가 아니라 두 점의 좌표 자체이므로
/// SelectWindow/SelectCrossingWindow는 호출하지 않는다. 인터랙티브라 InvokeInCommandContextAsync로
/// 실행한다.
/// </summary>
internal sealed class AcquirePlotWindowHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public AcquirePlotWindowHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.AcquirePlotWindow;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var outcome = await _dispatcher
            .InvokeInCommandContextAsync(RunAcquire, cancellationToken)
            .ConfigureAwait(false);

        return outcome.Kind switch
        {
            SelectionOutcomeKind.Selected => IpcHandlerResult.Ok(outcome.Response),
            SelectionOutcomeKind.Cancelled => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.SelectionCancelled, "Plot window selection was cancelled.")),
            SelectionOutcomeKind.NoActiveDocument => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            _ => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.ApiExecutionFailed, outcome.ErrorMessage ?? "Plot window selection failed."))
        };
    }

    // AutoCAD Command Context 안에서만 실행된다.
    private static SelectionOutcome<AcquirePlotWindowResponse> RunAcquire()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return SelectionOutcome<AcquirePlotWindowResponse>.NoActiveDocument();
        }

        using var documentLock = document.LockDocument();
        var editor = document.Editor;

        var firstCorner = editor.GetPoint("\nPlot 영역의 첫 번째 모서리를 지정하세요: ");
        if (firstCorner.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<AcquirePlotWindowResponse>.Cancelled();
        }

        if (firstCorner.Status != PromptStatus.OK)
        {
            return SelectionOutcome<AcquirePlotWindowResponse>.Error($"AutoCAD point input failed with status {firstCorner.Status}.");
        }

        var secondCorner = editor.GetCorner("\nPlot 영역의 반대쪽 모서리를 지정하세요: ", firstCorner.Value);
        if (secondCorner.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<AcquirePlotWindowResponse>.Cancelled();
        }

        if (secondCorner.Status != PromptStatus.OK)
        {
            return SelectionOutcome<AcquirePlotWindowResponse>.Error($"AutoCAD point input failed with status {secondCorner.Status}.");
        }

        var minX = System.Math.Min(firstCorner.Value.X, secondCorner.Value.X);
        var minY = System.Math.Min(firstCorner.Value.Y, secondCorner.Value.Y);
        var maxX = System.Math.Max(firstCorner.Value.X, secondCorner.Value.X);
        var maxY = System.Math.Max(firstCorner.Value.Y, secondCorner.Value.Y);

        var window = new CadPlotWindowDto(minX, minY, maxX, maxY);
        return SelectionOutcome<AcquirePlotWindowResponse>.Selected(new AcquirePlotWindowResponse(window));
    }
}
