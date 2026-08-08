using System;

namespace CADWorkAssistant.Core.Parapet;

/// <summary>측면 + 상부면 계산 결과와 그 근거가 되는 구성 값을 함께 담는다 (Milestone 4 §33-34, §45-47).</summary>
public sealed class ParapetResult
{
    public ParapetResult(
        double sourceLengthMeters,
        double heightMeters,
        ParapetFaceMode faceMode,
        int faceMultiplier,
        double sideAreaSquareMeters,
        bool topIncluded,
        double topWidthMeters,
        double topAreaSquareMeters,
        double totalAreaSquareMeters,
        DateTimeOffset createdAt)
    {
        SourceLengthMeters = sourceLengthMeters;
        HeightMeters = heightMeters;
        FaceMode = faceMode;
        FaceMultiplier = faceMultiplier;
        SideAreaSquareMeters = sideAreaSquareMeters;
        TopIncluded = topIncluded;
        TopWidthMeters = topWidthMeters;
        TopAreaSquareMeters = topAreaSquareMeters;
        TotalAreaSquareMeters = totalAreaSquareMeters;
        CreatedAt = createdAt;
    }

    public double SourceLengthMeters { get; }

    public double HeightMeters { get; }

    public ParapetFaceMode FaceMode { get; }

    /// <summary>Single=1, Both=2 - ViewModel이 매직넘버 없이 산식을 만들 수 있게 여기 담아 보낸다 (§36).</summary>
    public int FaceMultiplier { get; }

    /// <summary>측면 총 면적 - Both면 이미 ×2가 적용된 값이다.</summary>
    public double SideAreaSquareMeters { get; }

    public bool TopIncluded { get; }

    /// <summary>TopIncluded가 false면 0.</summary>
    public double TopWidthMeters { get; }

    /// <summary>TopIncluded가 false면 0.</summary>
    public double TopAreaSquareMeters { get; }

    public double TotalAreaSquareMeters { get; }

    public DateTimeOffset CreatedAt { get; }
}
