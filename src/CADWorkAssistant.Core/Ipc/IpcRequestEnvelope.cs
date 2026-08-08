using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CADWorkAssistant.Core.Ipc;

/// <summary>
/// Named Pipe 위에 오가는 요청 하나. Payload는 MessageType에 따라 의미가 달라지는 JSON object다 (§12).
/// </summary>
public sealed class IpcRequestEnvelope
{
    [JsonConstructor]
    public IpcRequestEnvelope(int protocolVersion, string requestId, string messageType, JsonElement? payload)
    {
        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        MessageType = messageType;
        Payload = payload;
    }

    public static IpcRequestEnvelope Create(string messageType, object? payload = null)
    {
        JsonElement? element = payload is null
            ? null
            : JsonSerializer.SerializeToElement(payload, payload.GetType(), IpcJson.Options);

        return new IpcRequestEnvelope(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid().ToString("n"),
            messageType,
            element);
    }

    public int ProtocolVersion { get; }

    public string RequestId { get; }

    public string MessageType { get; }

    public JsonElement? Payload { get; }

    public string ToJson() => JsonSerializer.Serialize(this, IpcJson.Options);

    public static IpcRequestEnvelope FromJson(string json) =>
        JsonSerializer.Deserialize<IpcRequestEnvelope>(json, IpcJson.Options)
        ?? throw new JsonException("Request envelope deserialized to null.");
}
