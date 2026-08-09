using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>Milestone 12 §36-37 - 새 문자를 놓을 위치를 AutoCAD에서 점 하나로 받는다. AcquirePlotWindow
/// (Milestone 11)와 같은 이유로 별도 명령이다 - Desktop은 좌표를 숫자로 입력받지 않는다.</summary>
internal sealed class AcquireTextInsertionPointHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public AcquireTextInsertionPointHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.AcquireTextInsertionPoint;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var outcome = await _dispatcher.InvokeInCommandContextAsync(RunAcquire, cancellationToken).ConfigureAwait(false);

        return outcome.Kind switch
        {
            SelectionOutcomeKind.Selected => IpcHandlerResult.Ok(outcome.Response),
            SelectionOutcomeKind.Cancelled => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.SelectionCancelled, "Insertion point selection was cancelled.")),
            SelectionOutcomeKind.NoActiveDocument => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            _ => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.ApiExecutionFailed, outcome.ErrorMessage ?? "Insertion point selection failed."))
        };
    }

    // AutoCAD Command Context 안에서만 실행된다.
    private static SelectionOutcome<AcquireTextInsertionPointResponse> RunAcquire()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return SelectionOutcome<AcquireTextInsertionPointResponse>.NoActiveDocument();
        }

        using var documentLock = document.LockDocument();
        var editor = document.Editor;

        var result = editor.GetPoint("\n문자를 삽입할 위치를 지정하세요: ");

        if (result.Status == PromptStatus.Cancel)
        {
            return SelectionOutcome<AcquireTextInsertionPointResponse>.Cancelled();
        }

        if (result.Status != PromptStatus.OK)
        {
            return SelectionOutcome<AcquireTextInsertionPointResponse>.Error($"AutoCAD point input failed with status {result.Status}.");
        }

        var point = new CadPointDto(result.Value.X, result.Value.Y, result.Value.Z);
        return SelectionOutcome<AcquireTextInsertionPointResponse>.Selected(new AcquireTextInsertionPointResponse(point));
    }
}
