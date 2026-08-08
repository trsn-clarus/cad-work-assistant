using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>실제 Entity.Visible 변경은 없다 - 어떤 Handle을 숨겼다고 "기억"만 해서 RestoreVisibility
/// round-trip을 검증할 수 있게 한다.</summary>
internal sealed class FakeIsolateObjectsHandler : IIpcRequestHandler
{
    private readonly FakeDrawingState _state;

    public FakeIsolateObjectsHandler(FakeDrawingState state)
    {
        _state = state;
    }

    public string MessageType => IpcMessageTypes.IsolateObjects;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<IsolateObjectsRequest>(IpcJson.Options);
        if (request is null)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "IsolateObjects requires handles.")));
        }

        _state.HiddenObjectHandles ??= new HashSet<string>();
        _state.HiddenObjectHandles.UnionWith(request.Handles);

        return Task.FromResult(IpcHandlerResult.Ok(payload: null));
    }
}
