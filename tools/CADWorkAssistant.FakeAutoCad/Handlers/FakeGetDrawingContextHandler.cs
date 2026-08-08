using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

internal sealed class FakeGetDrawingContextHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeGetDrawingContextHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.GetDrawingContext;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var context = new DrawingContext(
            documentDisplayName: _scenario.DrawingDisplayName,
            fullPath: _scenario.DrawingFullPath,
            isSaved: _scenario.IsSaved,
            isReadOnly: _scenario.IsReadOnly,
            layout: _scenario.Layout,
            units: _scenario.Unit,
            documentCount: _scenario.DocumentCount);

        return Task.FromResult(IpcHandlerResult.Ok(context));
    }
}
