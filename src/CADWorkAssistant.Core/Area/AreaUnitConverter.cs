using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Area;

/// <summary>
/// 면적 단위 변환. Length의 선형 계수를 그대로 재사용하되 제곱해서 적용한다 - mm² → m²는
/// 1,000,000으로 나누는 것이지 1,000으로 나누는 게 아니다 (§21). Unitless/Other는 Length와 마찬가지로
/// 임의로 변환하지 않는다 (§24).
/// </summary>
public static class AreaUnitConverter
{
    public static bool TryConvertToSquareMeters(double rawValue, DrawingUnit unit, out double squareMeters)
    {
        var factor = DrawingUnitConversion.MetersPerUnit(unit);
        if (factor is null)
        {
            squareMeters = 0;
            return false;
        }

        squareMeters = rawValue * factor.Value * factor.Value;
        return true;
    }
}
