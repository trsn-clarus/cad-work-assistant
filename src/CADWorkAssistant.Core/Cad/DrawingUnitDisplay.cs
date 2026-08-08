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
}
