using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Length;

/// <summary>
/// 길이 단위 변환. mm/cm/m는 실무에서 반드시 필요하고, inch/feet/yard/mile도 함께 지원한다 (§21).
/// Unitless/Other는 임의로 변환하지 않는다 - 실패를 반환할 뿐 예외를 던지지 않는다. Unitless 도면은
/// 실무에서 드물지 않게 나오는 정상적인 입력이라 예외 처리보다 값 있는 반환이 자연스럽다 (§22).
/// </summary>
public static class LengthUnitConverter
{
    public static bool TryConvertToMeters(double rawValue, DrawingUnit unit, out double meters)
    {
        var factor = MetersPerUnit(unit);
        if (factor is null)
        {
            meters = 0;
            return false;
        }

        meters = rawValue * factor.Value;
        return true;
    }

    /// <summary>Unitless/Other이면 null - "미터로 환산할 수 없다"는 뜻이지 0이 아니다.</summary>
    private static double? MetersPerUnit(DrawingUnit unit) => unit switch
    {
        DrawingUnit.Millimeters => 0.001,
        DrawingUnit.Centimeters => 0.01,
        DrawingUnit.Decimeters => 0.1,
        DrawingUnit.Meters => 1.0,
        DrawingUnit.Kilometers => 1000.0,
        DrawingUnit.Inches => 0.0254,
        DrawingUnit.Feet => 0.3048,
        DrawingUnit.Yards => 0.9144,
        DrawingUnit.Miles => 1609.344,
        _ => null
    };
}
