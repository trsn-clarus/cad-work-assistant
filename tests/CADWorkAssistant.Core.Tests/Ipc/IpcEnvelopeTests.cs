using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.Core.Tests.Ipc;

public class IpcEnvelopeTests
{
    private sealed record SamplePayload(string Name, int Count);

    [Fact]
    public void RequestEnvelope_RoundTripsThroughJson()
    {
        var request = IpcRequestEnvelope.Create(IpcMessageTypes.Ping, new SamplePayload("A-WALL", 4));

        var json = request.ToJson();
        var restored = IpcRequestEnvelope.FromJson(json);

        Assert.Equal(request.RequestId, restored.RequestId);
        Assert.Equal(request.ProtocolVersion, restored.ProtocolVersion);
        Assert.Equal(IpcMessageTypes.Ping, restored.MessageType);
        Assert.NotNull(restored.Payload);
    }

    [Fact]
    public void RequestEnvelope_Create_GeneratesUniqueRequestIds()
    {
        var first = IpcRequestEnvelope.Create(IpcMessageTypes.Ping);
        var second = IpcRequestEnvelope.Create(IpcMessageTypes.Ping);

        Assert.NotEqual(first.RequestId, second.RequestId);
    }

    [Fact]
    public void ResponseEnvelope_Ok_DeserializesPayloadBackToOriginalShape()
    {
        var response = IpcResponseEnvelope.Ok("req-1", new SamplePayload("A-WALL", 4));

        var json = response.ToJson();
        var restored = IpcResponseEnvelope.FromJson(json);
        var payload = restored.DeserializePayload<SamplePayload>();

        Assert.True(restored.Success);
        Assert.Equal("A-WALL", payload!.Name);
        Assert.Equal(4, payload.Count);
    }

    [Fact]
    public void ResponseEnvelope_Fail_PreservesErrorCodeAndMessage()
    {
        var response = IpcResponseEnvelope.Fail("req-2", new IpcError(IpcErrorCode.NoActiveDocument, "No document is open."));

        var json = response.ToJson();
        var restored = IpcResponseEnvelope.FromJson(json);

        Assert.False(restored.Success);
        Assert.Equal(IpcErrorCode.NoActiveDocument, restored.Error!.Code);
        Assert.Equal("No document is open.", restored.Error.Message);
        Assert.Null(restored.Payload);
    }
}
