using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

internal sealed class FakeGetDrawingOverviewHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;
    private readonly FakeDrawingState _state;

    public FakeGetDrawingOverviewHandler(SimulationScenario scenario, FakeDrawingState state)
    {
        _scenario = scenario;
        _state = state;
    }

    public string MessageType => IpcMessageTypes.GetDrawingOverview;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var bounds = BoundsAggregator.Aggregate(System.Linq.Enumerable.Select(_scenario.DrawingObjects, o => o.Bounds));
        var response = new DrawingOverviewResponse(bounds, _scenario.DrawingObjects.Count, _state.CurrentLayers.Count);
        return Task.FromResult(IpcHandlerResult.Ok(response));
    }
}
