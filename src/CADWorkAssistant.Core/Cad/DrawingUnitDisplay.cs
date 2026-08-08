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
}
