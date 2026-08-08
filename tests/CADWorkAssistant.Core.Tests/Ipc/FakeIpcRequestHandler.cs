using System.Text.Json;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.Core.Tests.Ipc;

/// <summary>테스트 전용 Handler. AutoCAD 없이 IpcRequestDispatcher를 검증하기 위한 것.</summary>
internal sealed class FakeIpcRequestHandler : IIpcRequestHandler
{
    private readonly Func<JsonElement?, CancellationToken, Task<IpcHandlerResult>> _handle;

    public FakeIpcRequestHandler(string messageType, Func<JsonElement?, CancellationToken, Task<IpcHandlerResult>> handle)
    {
        MessageType = messageType;
        _handle = handle;
    }

    public static FakeIpcRequestHandler ReturningOk(string messageType, object? payload = null) =>
        new(messageType, (_, _) => Task.FromResult(IpcHandlerResult.Ok(payload)));

    public static FakeIpcRequestHandler Throwing(string messageType, Exception exception) =>
        new(messageType, (_, _) => throw exception);

    public string MessageType { get; }

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken) =>
        _handle(payload, cancellationToken);
}
