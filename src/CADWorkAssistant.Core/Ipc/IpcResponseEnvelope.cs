using System.Text.Json;
using System.Text.Json.Serialization;

namespace CADWorkAssistant.Core.Ipc;

/// <summary>요청 하나에 대한 응답. RequestId는 항상 요청과 동일한 값을 그대로 돌려준다 (§14).</summary>
public sealed class IpcResponseEnvelope
{
    [JsonConstructor]
    public IpcResponseEnvelope(int protocolVersion, string requestId, bool success, JsonElement? payload, IpcError? error)
    {
        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        Success = success;
        Payload = payload;
        Error = error;
    }

    public static IpcResponseEnvelope Ok(string requestId, object? payload) =>
        new(
            IpcProtocol.CurrentVersion,
            requestId,
            true,
            payload is null ? null : JsonSerializer.SerializeToElement(payload, payload.GetType(), IpcJson.Options),
            null);

    public static IpcResponseEnvelope Fail(string requestId, IpcError error) =>
        new(IpcProtocol.CurrentVersion, requestId, false, null, error);

    public int ProtocolVersion { get; }

    public string RequestId { get; }

    public bool Success { get; }

    public JsonElement? Payload { get; }

    public IpcError? Error { get; }

    public T? DeserializePayload<T>() => Payload is null ? default : Payload.Value.Deserialize<T>(IpcJson.Options);

    public string ToJson() => JsonSerializer.Serialize(this, IpcJson.Options);

    public static IpcResponseEnvelope FromJson(string json) =>
        JsonSerializer.Deserialize<IpcResponseEnvelope>(json, IpcJson.Options)
        ?? throw new JsonException("Response envelope deserialized to null.");
}
