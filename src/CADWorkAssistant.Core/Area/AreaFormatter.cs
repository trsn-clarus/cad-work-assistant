using System.Globalization;

namespace CADWorkAssistant.Core.Area;

/// <summary>
/// 내부 계산은 double 원본 정밀도를 유지하고, 표시할 때만 반올림한다 (Length와 동일한 원칙).
/// 면적은 소수점 2자리를 기본으로 한다 - 실무에서 길이(3자리)보다 낮은 정밀도로 충분하다 (§29).
/// </summary>
public static class AreaFormatter
{
    public const int DefaultDecimalPlaces = 2;

    public static string FormatSquareMeters(double squareMeters, int decimalPlaces = DefaultDecimalPlaces) =>
        squareMeters.ToString("N" + decimalPlaces, CultureInfo.InvariantCulture);

    public static string FormatSquareMetersWithUnit(double squareMeters, int decimalPlaces = DefaultDecimalPlaces) =>
        FormatSquareMeters(squareMeters, decimalPlaces) + " m²";
}
