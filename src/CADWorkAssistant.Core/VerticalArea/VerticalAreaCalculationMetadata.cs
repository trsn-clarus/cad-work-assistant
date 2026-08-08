using System.Text.Json;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.Core.VerticalArea;

/// <summary>
/// Vertical Area 계산에 실제로 쓰인 입력값 - <c>QuantityRecord.CalculationMetadataJson</c>에 저장되어
/// 나중에 <see cref="VerticalAreaCalculator"/>와 같은 산식(L × H)을 그대로 다시 계산해 저장된 결과와
/// 일치하는지 검증할 수 있게 한다(Milestone 7 Verification Engine). 사람이 읽는 `CalculationExpression`
/// 문자열과 달리 이 값은 기계가 재계산하기 위한 것이다 - 문자열 산식을 파싱하지 않는다(§25).
/// 이미 미터로 환산된 값만 저장한다 - `RawValue`/`SourceUnit`이 Vertical Area/Parapet에서는 항상
/// 미터라는 기존 관례(`docs/QUANTITY_COMPOSITION.md`)와 같은 이유다.
/// </summary>
public sealed class VerticalAreaCalculationMetadata
{
    public VerticalAreaCalculationMetadata(double sourceLengthMeters, double heightMeters)
    {
        SourceLengthMeters = sourceLengthMeters;
        HeightMeters = heightMeters;
    }

    public double SourceLengthMeters { get; }

    public double HeightMeters { get; }

    public string ToJson() => JsonSerializer.Serialize(this, IpcJson.Options);

    public static VerticalAreaCalculationMetadata? TryParse(string? json)
    {
        if (json is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<VerticalAreaCalculationMetadata>(json, IpcJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
