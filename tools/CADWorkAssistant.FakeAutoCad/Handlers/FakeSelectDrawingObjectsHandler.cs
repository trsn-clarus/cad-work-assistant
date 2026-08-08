using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>실제 AutoCadPlugin의 SelectDrawingObjectsHandler와 같은 MessageType/Envelope를 쓴다.
/// Window/Crossing 구분 없이 같은 Scenario.DrawingObjects를 돌려준다 - Fake는 실제 기하학적
/// 판정을 하지 않는다(§69, 진짜 WBLOCK 정확성처럼 Fake가 대신 증명할 수 없는 영역).</summary>
internal sealed class FakeSelectDrawingObjectsHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeSelectDrawingObjectsHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.SelectDrawingObjects;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<SelectDrawingObjectsRequest>(IpcJson.Options);
        if (request is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "SelectDrawingObjects requires a selection mode."));
        }

        switch (_scenario.DrawingSelectionBehavior)
        {
            case SelectionBehavior.Cancelled:
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.SelectionCancelled, "Selection was cancelled."));

            case SelectionBehavior.ReturnError:
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, "Simulated AutoCAD internal error."));

            case SelectionBehavior.HangForever:
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.Timeout, "unreachable"));

            case SelectionBehavior.DisconnectBeforeResponding:
                Environment.Exit(0);
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InternalError, "unreachable"));

            case SelectionBehavior.ReturnObjects:
            default:
                return IpcHandlerResult.Ok(new DrawingSelectionResponse(_scenario.DrawingObjects));
        }
    }
}
