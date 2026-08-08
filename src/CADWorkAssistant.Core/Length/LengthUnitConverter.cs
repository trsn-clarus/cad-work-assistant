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
        var factor = DrawingUnitConversion.MetersPerUnit(unit);
        if (factor is null)
        {
            meters = 0;
            return false;
        }

        meters = rawValue * factor.Value;
        return true;
    }
}
