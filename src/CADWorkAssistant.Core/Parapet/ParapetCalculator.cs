using System;
using CADWorkAssistant.Core.VerticalArea;

namespace CADWorkAssistant.Core.Parapet;

/// <summary>
/// Parapet은 별도 계산 엔진이 아니라 VerticalAreaCalculator를 두 번 재사용한 조합이다 (Milestone 4 §23):
/// 한 번은 측면(L×H)에, 한 번은 상부면(L×Width - Width를 Height 자리에 그대로 넣는다, 둘 다
/// "길이 × 다른 한 변" 계산이라 수식이 같다)에 쓴다.
/// </summary>
public static class ParapetCalculator
{
    public static int FaceMultiplier(ParapetFaceMode mode) => mode switch
    {
        ParapetFaceMode.Single => 1,
        ParapetFaceMode.Both => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    public static ParapetValidationResult Validate(double heightRawValue, bool topIncluded, double topWidthRawValue) =>
        new(isHeightValid: heightRawValue > 0, isTopWidthValid: !topIncluded || topWidthRawValue > 0);

    public static ParapetResult Calculate(ParapetInput input, DateTimeOffset createdAt)
    {
        var validation = Validate(input.HeightRawValue, input.TopIncluded, input.TopWidthRawValue);
        if (!validation.IsValid)
        {
            throw new ArgumentException("Parapet input failed validation - caller must check Validate() first.", nameof(input));
        }

        var side = VerticalAreaCalculator.Calculate(input.SourceLengthMeters, input.HeightRawValue, input.HeightUnit, createdAt);
        var multiplier = FaceMultiplier(input.FaceMode);
        var sideTotal = side.AreaSquareMeters * multiplier;

        var topWidthMeters = 0.0;
        var topArea = 0.0;
        if (input.TopIncluded)
        {
            var top = VerticalAreaCalculator.Calculate(input.SourceLengthMeters, input.TopWidthRawValue, input.TopWidthUnit, createdAt);
            topWidthMeters = top.HeightMeters;
            topArea = top.AreaSquareMeters;
        }

        var total = sideTotal + topArea;

        return new ParapetResult(
            input.SourceLengthMeters,
            side.HeightMeters,
            input.FaceMode,
            multiplier,
            sideTotal,
            input.TopIncluded,
            topWidthMeters,
            topArea,
            total,
            createdAt);
    }
}
