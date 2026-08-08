using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>실제 View 조작이 없으므로 성공만 응답한다 - Zoom의 시각적 정확성은 Real AutoCAD에서만
/// 검증 가능하다(§9, §74). 여기서는 IPC round-trip(요청/응답 계약)만 검증한다.</summary>
internal sealed class FakeZoomExtentsHandler : IIpcRequestHandler
{
    public string MessageType => IpcMessageTypes.ZoomExtents;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken) =>
        Task.FromResult(IpcHandlerResult.Ok(payload: null));
}
