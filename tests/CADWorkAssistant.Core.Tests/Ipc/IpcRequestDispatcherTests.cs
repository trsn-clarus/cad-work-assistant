using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.Core.Tests.Ipc;

public class IpcRequestDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_RoutesToMatchingHandler()
    {
        var handler = FakeIpcRequestHandler.ReturningOk("Ping", new { pong = true });
        var dispatcher = new IpcRequestDispatcher(new[] { handler });
        var request = IpcRequestEnvelope.Create("Ping");

        var response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(request.RequestId, response.RequestId);
    }

    [Fact]
    public async Task DispatchAsync_PreservesRequestId()
    {
        var dispatcher = new IpcRequestDispatcher(new[] { FakeIpcRequestHandler.ReturningOk("Ping") });
        var request = IpcRequestEnvelope.Create("Ping");

        var response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.Equal(request.RequestId, response.RequestId);
    }

    [Fact]
    public async Task DispatchAsync_UnknownMessageType_ReturnsInvalidRequest()
    {
        var dispatcher = new IpcRequestDispatcher(Array.Empty<FakeIpcRequestHandler>());
        var request = IpcRequestEnvelope.Create("SomethingThatDoesNotExist");

        var response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task DispatchAsync_ProtocolVersionMismatch_ReturnsUnsupportedProtocol()
    {
        var dispatcher = new IpcRequestDispatcher(new[] { FakeIpcRequestHandler.ReturningOk("Ping") });
        var request = IpcRequestEnvelope.FromJson(
            $$"""{"protocolVersion":999,"requestId":"r1","messageType":"Ping","payload":null}""");

        var response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.UnsupportedProtocol, response.Error!.Code);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_ReturnsInternalErrorWithoutLeakingToUserMessage()
    {
        var dispatcher = new IpcRequestDispatcher(new[]
        {
            FakeIpcRequestHandler.Throwing("Boom", new InvalidOperationException("raw AutoCAD exception detail"))
        });
        var request = IpcRequestEnvelope.Create("Boom");

        var response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InternalError, response.Error!.Code);
        Assert.DoesNotContain("raw AutoCAD exception detail", response.Error.Message);
        Assert.Contains("raw AutoCAD exception detail", response.Error.TechnicalDetail);
    }

    [Fact]
    public async Task DispatchAsync_HandlerReturnsExplicitFailure_PropagatesErrorCode()
    {
        var handler = new FakeIpcRequestHandler(
            "GetDrawingContext",
            (_, _) => Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "No document is open."))));
        var dispatcher = new IpcRequestDispatcher(new[] { handler });
        var request = IpcRequestEnvelope.Create("GetDrawingContext");

        var response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.NoActiveDocument, response.Error!.Code);
    }
}
