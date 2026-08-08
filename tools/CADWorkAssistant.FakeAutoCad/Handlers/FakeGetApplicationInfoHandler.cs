using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

internal sealed class FakeGetApplicationInfoHandler : IIpcRequestHandler
{
    private readonly int _processId;
    private readonly SimulationScenario _scenario;

    public FakeGetApplicationInfoHandler(int processId, SimulationScenario scenario)
    {
        _processId = processId;
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.GetApplicationInfo;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var info = new AutoCadInstanceInfo(
            product: "CAD Work Assistant Simulation",
            version: $"scenario:{_scenario.Name}",
            processId: _processId,
            pluginVersion: "fake-0.1.0",
            protocolVersion: IpcProtocol.CurrentVersion,
            isSimulated: true);

        return Task.FromResult(IpcHandlerResult.Ok(info));
    }
}
