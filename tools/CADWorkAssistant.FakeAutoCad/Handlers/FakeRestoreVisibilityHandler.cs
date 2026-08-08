using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>실제 Plugin의 RestoreVisibilityHandler와 같은 규칙 - "전부 On"이 아니라 스냅샷 시점
/// 상태로 정확히 되돌린다(§45-46). Object Isolation은 Fake에 실제 표시할 게 없으니 상태만 지운다.</summary>
internal sealed class FakeRestoreVisibilityHandler : IIpcRequestHandler
{
    private readonly FakeDrawingState _state;

    public FakeRestoreVisibilityHandler(FakeDrawingState state)
    {
        _state = state;
    }

    public string MessageType => IpcMessageTypes.RestoreVisibility;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        if (_state.OriginalLayerOnState is { } originalLayerStates)
        {
            foreach (var entry in originalLayerStates)
            {
                _state.SetLayerOn(entry.Key, entry.Value);
            }
        }

        _state.Clear();
        return Task.FromResult(IpcHandlerResult.Ok(payload: null));
    }
}
