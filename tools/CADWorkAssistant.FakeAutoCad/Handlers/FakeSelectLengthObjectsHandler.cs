using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>
/// 실제 AutoCadPlugin의 SelectLengthObjectsHandler와 같은 MessageType/Envelope를 쓴다 - 프로토콜은
/// 하나뿐이다. 사용자 선택을 실제로 기다리는 대신 Scenario에 미리 정해둔 데이터로 즉시 응답한다.
/// </summary>
internal sealed class FakeSelectLengthObjectsHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeSelectLengthObjectsHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.SelectLengthObjects;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        switch (_scenario.Behavior)
        {
            case SelectionBehavior.Cancelled:
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.SelectionCancelled, "Selection was cancelled."));

            case SelectionBehavior.ReturnError:
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, "Simulated AutoCAD internal error."));

            case SelectionBehavior.HangForever:
                // 서버(AutoCadPipeServer)의 RequestTimeoutMs 또는 클라이언트의 request timeout이
                // 걸릴 때까지 절대 응답하지 않는다 - 둘 중 하나가 Desktop을 안전하게 실패시켜야 한다.
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.Timeout, "unreachable"));

            case SelectionBehavior.DisconnectBeforeResponding:
                // AutoCAD가 응답 직전에 죽은 상황을 흉내낸다 - 프로세스 자체를 종료한다.
                Environment.Exit(0);
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InternalError, "unreachable"));

            case SelectionBehavior.ReturnObjects:
            default:
                var response = new LengthSelectionResponse(_scenario.Objects, _scenario.ExcludedObjectTypeNames, _scenario.Unit);
                return IpcHandlerResult.Ok(response);
        }
    }
}
