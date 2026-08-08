using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// 연결 확인용 heartbeat. 의도적으로 AutoCAD API를 전혀 건드리지 않는다 - 2초 간격으로 계속 호출돼도
/// AutoCAD 문서/메인 스레드에 아무 부담이 없어야 하기 때문 (§26, §45, §46).
/// </summary>
internal sealed class PingHandler : IIpcRequestHandler
{
    public string MessageType => IpcMessageTypes.Ping;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken) =>
        Task.FromResult(IpcHandlerResult.Ok(new { pong = true, serverTimeUtc = DateTimeOffset.UtcNow }));
}
