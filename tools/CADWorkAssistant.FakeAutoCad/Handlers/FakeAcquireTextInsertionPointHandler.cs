using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

internal sealed class FakeAcquireTextInsertionPointHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeAcquireTextInsertionPointHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.AcquireTextInsertionPoint;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        switch (_scenario.TextInsertionPointBehavior)
        {
            case SelectionBehavior.Cancelled:
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.SelectionCancelled, "Insertion point selection was cancelled."));

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
                return IpcHandlerResult.Ok(new AcquireTextInsertionPointResponse(_scenario.TextInsertionPoint));
        }
    }
}
