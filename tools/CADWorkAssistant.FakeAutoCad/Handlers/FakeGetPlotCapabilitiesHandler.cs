using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>FakeGetLayersHandler/FakeGetDrawingContextHandler와 같은 단순 pass-through 패턴 -
/// Scenario에 미리 담긴 장치/용지/스타일/Layout 목록을 그대로 돌려준다. 항상 성공한다(§16 계열
/// Read-only 조회는 다른 Fake Handler들도 실패 Behavior를 두지 않는다).</summary>
internal sealed class FakeGetPlotCapabilitiesHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeGetPlotCapabilitiesHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.GetPlotCapabilities;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var response = new PlotCapabilitiesResponse(
            _scenario.PlotDevices,
            _scenario.PlotMedia,
            _scenario.PlotColorDependentStyleSheets,
            _scenario.PlotNamedStyleSheets,
            _scenario.PlotCurrentStyleMode,
            _scenario.PlotLayouts);

        return Task.FromResult(IpcHandlerResult.Ok(response));
    }
}
