using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>
/// 실제 AutoCadPlugin의 SelectAreaObjectsHandler와 같은 MessageType/Envelope를 쓴다 - 프로토콜은
/// 하나뿐이다. FakeSelectLengthObjectsHandler와 동일한 구조를 그대로 따른다 (Milestone 3 §31).
/// </summary>
internal sealed class FakeSelectAreaObjectsHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeSelectAreaObjectsHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.SelectAreaObjects;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        switch (_scenario.AreaBehavior)
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
                var response = new AreaSelectionResponse(_scenario.AreaObjects, _scenario.AreaExcludedObjectTypeNames, _scenario.Unit);
                return IpcHandlerResult.Ok(response);
        }
    }
}
