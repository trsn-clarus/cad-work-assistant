using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

internal sealed class FakePingHandler : IIpcRequestHandler
{
    public string MessageType => IpcMessageTypes.Ping;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken) =>
        Task.FromResult(IpcHandlerResult.Ok(new { pong = true, serverTimeUtc = DateTimeOffset.UtcNow }));
}
