using System;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.VerticalArea;

/// <summary>
/// A = L × H 계산 결과. Parapet은 이 타입을 두 번 재사용한다 - 한 번은 측면(L×H), 한 번은
/// 상부면(L×Width, Width를 Height 자리에 넣는다) 계산에 (Milestone 4 §23, §32).
/// </summary>
public sealed class VerticalAreaResult
{
    public VerticalAreaResult(
        double sourceLengthMeters,
        double heightRawValue,
        DrawingUnit heightUnit,
        double heightMeters,
        double areaSquareMeters,
        DateTimeOffset createdAt)
    {
        SourceLengthMeters = sourceLengthMeters;
        HeightRawValue = heightRawValue;
        HeightUnit = heightUnit;
        HeightMeters = heightMeters;
        AreaSquareMeters = areaSquareMeters;
        CreatedAt = createdAt;
    }

    public double SourceLengthMeters { get; }

    /// <summary>사용자가 입력한 원본 값 (예: 100, 단위는 HeightUnit) - 화면에 그대로 되돌려 보여줄 때 쓴다.</summary>
    public double HeightRawValue { get; }

    public DrawingUnit HeightUnit { get; }

    /// <summary>정규화된 값 (미터) - 실제 계산은 전부 이 값으로 한다.</summary>
    public double HeightMeters { get; }

    public double AreaSquareMeters { get; }

    public DateTimeOffset CreatedAt { get; }
}
