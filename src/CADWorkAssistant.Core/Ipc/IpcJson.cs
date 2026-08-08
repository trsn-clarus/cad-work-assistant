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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // System.Text.Json은 기본적으로 NaN/Infinity를 유효하지 않은 JSON 숫자로 보고 직렬화에서
        // 예외를 던진다. Area는 AutoCAD가 면적 계산에 실패했음을 NaN으로 전달한다(Milestone 3
        // CadAreaObjectDto.RawArea) - "NaN"/"Infinity" 문자열 리터럴로 왕복시킨다.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
}
