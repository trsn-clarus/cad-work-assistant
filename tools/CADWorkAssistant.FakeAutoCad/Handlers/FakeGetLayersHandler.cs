using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

internal sealed class FakeGetLayersHandler : IIpcRequestHandler
{
    private readonly FakeDrawingState _state;

    public FakeGetLayersHandler(FakeDrawingState state)
    {
        _state = state;
    }

    public string MessageType => IpcMessageTypes.GetLayers;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken) =>
        Task.FromResult(IpcHandlerResult.Ok(new GetLayersResponse(_state.CurrentLayers)));
}
