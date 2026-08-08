using System;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.VerticalArea;

/// <summary>
/// A = L × H. 중간값을 먼저 반올림하지 않는다 - 표시 단계(Formatter)에서만 반올림한다 (Milestone 4 §60).
/// </summary>
public static class VerticalAreaCalculator
{
    public static VerticalAreaValidation Validate(double heightRawValue) =>
        heightRawValue > 0 ? VerticalAreaValidation.Valid : VerticalAreaValidation.HeightNotPositive;

    public static VerticalAreaResult Calculate(
        double sourceLengthMeters,
        double heightRawValue,
        DrawingUnit heightUnit,
        DateTimeOffset createdAt)
    {
        if (Validate(heightRawValue) != VerticalAreaValidation.Valid)
        {
            throw new ArgumentException($"Height ({heightRawValue}) must be positive.", nameof(heightRawValue));
        }

        var factor = DrawingUnitConversion.MetersPerUnit(heightUnit) ??
            throw new ArgumentException($"Height unit {heightUnit} cannot be converted to meters.", nameof(heightUnit));

        var heightMeters = heightRawValue * factor;
        var areaSquareMeters = sourceLengthMeters * heightMeters;

        return new VerticalAreaResult(sourceLengthMeters, heightRawValue, heightUnit, heightMeters, areaSquareMeters, createdAt);
    }
}
