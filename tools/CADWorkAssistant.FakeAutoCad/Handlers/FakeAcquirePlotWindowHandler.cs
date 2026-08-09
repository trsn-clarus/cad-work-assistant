using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>FakeSelectDrawingObjectsHandler와 같은 SelectionBehavior 분기 패턴을 그대로 재사용한다
/// (Milestone 11 §13).</summary>
internal sealed class FakeAcquirePlotWindowHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeAcquirePlotWindowHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.AcquirePlotWindow;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        switch (_scenario.PlotWindowBehavior)
        {
            case SelectionBehavior.Cancelled:
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.SelectionCancelled, "Plot window selection was cancelled."));

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
                return IpcHandlerResult.Ok(new AcquirePlotWindowResponse(_scenario.PlotWindow));
        }
    }
}
