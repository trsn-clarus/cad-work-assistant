using System.Globalization;

namespace CADWorkAssistant.Core.Length;

/// <summary>
/// 내부 계산은 double 원본 정밀도를 유지하고, 표시할 때만 반올림한다 (§24).
/// </summary>
public static class LengthFormatter
{
    public const int DefaultDecimalPlaces = 3;

    public static string FormatMeters(double meters, int decimalPlaces = DefaultDecimalPlaces) =>
        meters.ToString("N" + decimalPlaces, CultureInfo.InvariantCulture);

    public static string FormatMetersWithUnit(double meters, int decimalPlaces = DefaultDecimalPlaces) =>
        FormatMeters(meters, decimalPlaces) + " m";
}
