using System.Text.Json;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.Core.Parapet;

/// <summary>
/// Parapet 계산에 실제로 쓰인 입력값 - <see cref="VerticalArea.VerticalAreaCalculationMetadata"/>와 같은
/// 이유로 존재한다. <see cref="ParapetCalculator.FaceMultiplier"/>를 그대로 재사용해 측면/상부 면적을
/// 다시 계산할 수 있게 <c>FaceMode</c>(정수 배율이 아니라)를 저장한다 - 배율 자체를 저장하면 나중에
/// <see cref="ParapetCalculator"/>의 배율 규칙이 바뀌었을 때 저장된 배율이 조용히 낡아버린다.
/// </summary>
public sealed class ParapetCalculationMetadata
{
    public ParapetCalculationMetadata(
        double sourceLengthMeters,
        double heightMeters,
        ParapetFaceMode faceMode,
        bool topIncluded,
        double topWidthMeters)
    {
        SourceLengthMeters = sourceLengthMeters;
        HeightMeters = heightMeters;
        FaceMode = faceMode;
        TopIncluded = topIncluded;
        TopWidthMeters = topWidthMeters;
    }

    public double SourceLengthMeters { get; }

    public double HeightMeters { get; }

    public ParapetFaceMode FaceMode { get; }

    public bool TopIncluded { get; }

    /// <summary>TopIncluded가 false면 의미 없는 값 - 0으로 저장된다.</summary>
    public double TopWidthMeters { get; }

    public string ToJson() => JsonSerializer.Serialize(this, IpcJson.Options);

    public static ParapetCalculationMetadata? TryParse(string? json)
    {
        if (json is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ParapetCalculationMetadata>(json, IpcJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
