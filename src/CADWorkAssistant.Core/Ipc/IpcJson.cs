using System.Text.Json;
using System.Text.Json.Serialization;

namespace CADWorkAssistant.Core.Ipc;

/// <summary>Desktop과 AutoCAD Plugin이 동일하게 사용하는 직렬화 옵션. 여기서만 정의한다.</summary>
public static class IpcJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
