namespace CADWorkAssistant.Core.Cad;

/// <summary>DrawingUnit을 짧은 표시 문자열로 바꾼다. Desktop의 여러 화면이 공유한다 (DRY).</summary>
public static class DrawingUnitDisplay
{
    public static string Abbreviation(DrawingUnit unit) => unit switch
    {
        DrawingUnit.Unitless => "Unitless",
        DrawingUnit.Millimeters => "mm",
        DrawingUnit.Centimeters => "cm",
        DrawingUnit.Decimeters => "dm",
        DrawingUnit.Meters => "m",
        DrawingUnit.Kilometers => "km",
        DrawingUnit.Inches => "in",
        DrawingUnit.Feet => "ft",
        DrawingUnit.Yards => "yd",
        DrawingUnit.Miles => "mi",
        _ => "other"
    };

    /// <summary>면적 표시용 (예: "mm²"). Unitless/Other는 제곱 표기가 의미 없으므로 그대로 둔다 (Milestone 3 §22).</summary>
    public static string SquaredAbbreviation(DrawingUnit unit) => unit switch
    {
        DrawingUnit.Unitless => "Unitless",
        DrawingUnit.Other => "other",
        _ => Abbreviation(unit) + "²"
    };

    /// <summary><see cref="Abbreviation"/>의 역변환 - Milestone 7 Verification Engine이
    /// <c>QuantityRecord.SourceUnit</c>에 저장된 문자열("mm" 등)로부터 다시 변환 계수를 찾아야 해서
    /// 필요해졌다. Unitless/Other/인식 못하는 문자열은 false를 반환한다(둘 다 변환 계수가 없다 -
    /// <see cref="DrawingUnitConversion.MetersPerUnit"/>도 이 둘에 대해 null을 반환한다).</summary>
    public static bool TryParseAbbreviation(string abbreviation, out DrawingUnit unit)
    {
        switch (abbreviation)
        {
            case "mm": unit = DrawingUnit.Millimeters; return true;
            case "cm": unit = DrawingUnit.Centimeters; return true;
            case "dm": unit = DrawingUnit.Decimeters; return true;
            case "m": unit = DrawingUnit.Meters; return true;
            case "km": unit = DrawingUnit.Kilometers; return true;
            case "in": unit = DrawingUnit.Inches; return true;
            case "ft": unit = DrawingUnit.Feet; return true;
            case "yd": unit = DrawingUnit.Yards; return true;
            case "mi": unit = DrawingUnit.Miles; return true;
            case "Unitless": unit = DrawingUnit.Unitless; return true;
            default: unit = DrawingUnit.Other; return false;
        }
    }

    /// <summary><see cref="SquaredAbbreviation"/>의 역변환 (예: "mm²" → Millimeters).</summary>
    public static bool TryParseSquaredAbbreviation(string abbreviation, out DrawingUnit unit)
    {
        if (abbreviation == "Unitless")
        {
            unit = DrawingUnit.Unitless;
            return true;
        }

        if (abbreviation.EndsWith("²", System.StringComparison.Ordinal)
            && TryParseAbbreviation(abbreviation.Substring(0, abbreviation.Length - 1), out unit))
        {
            return true;
        }

        unit = DrawingUnit.Other;
        return false;
    }
}
