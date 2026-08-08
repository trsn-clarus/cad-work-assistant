using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>실제 Plugin의 SetLayerVisibilityHandler와 같은 규칙 - 처음 바뀌는 순간 전체 상태를
/// 스냅샷하고(§45), 현재 Layer는 Off 대상에서 제외한다(§44).</summary>
internal sealed class FakeSetLayerVisibilityHandler : IIpcRequestHandler
{
    private readonly FakeDrawingState _state;

    public FakeSetLayerVisibilityHandler(FakeDrawingState state)
    {
        _state = state;
    }

    public string MessageType => IpcMessageTypes.SetLayerVisibility;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<SetLayerVisibilityRequest>(IpcJson.Options);
        if (request is null)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "SetLayerVisibility requires at least one change.")));
        }

        _state.SnapshotLayerStateIfNeeded();

        foreach (var change in request.Changes)
        {
            if (!_state.LayerExists(change.LayerName))
            {
                continue;
            }

            if (!change.IsOn && _state.IsCurrentLayer(change.LayerName))
            {
                continue;
            }

            _state.SetLayerOn(change.LayerName, change.IsOn);
        }

        return Task.FromResult(IpcHandlerResult.Ok(payload: null));
    }
}
