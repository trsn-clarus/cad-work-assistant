using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Parapet;

/// <summary>Parapet 계산에 필요한 입력을 하나로 묶는다 - 매개변수 6개짜리 메서드 시그니처를 피한다.</summary>
public sealed class ParapetInput
{
    public ParapetInput(
        double sourceLengthMeters,
        double heightRawValue,
        DrawingUnit heightUnit,
        ParapetFaceMode faceMode,
        bool topIncluded,
        double topWidthRawValue,
        DrawingUnit topWidthUnit)
    {
        SourceLengthMeters = sourceLengthMeters;
        HeightRawValue = heightRawValue;
        HeightUnit = heightUnit;
        FaceMode = faceMode;
        TopIncluded = topIncluded;
        TopWidthRawValue = topWidthRawValue;
        TopWidthUnit = topWidthUnit;
    }

    public double SourceLengthMeters { get; }

    public double HeightRawValue { get; }

    public DrawingUnit HeightUnit { get; }

    public ParapetFaceMode FaceMode { get; }

    public bool TopIncluded { get; }

    /// <summary>TopIncluded가 false면 무시된다 (§38).</summary>
    public double TopWidthRawValue { get; }

    public DrawingUnit TopWidthUnit { get; }
}
