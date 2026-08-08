namespace CADWorkAssistant.Core.Cad;

/// <summary>
/// DrawingUnit → 미터 환산 계수. Length(선형)와 Area(면적, 제곱해서 사용)가 같은 표를 쓴다 -
/// 단위 하나 늘 때마다 두 곳을 따로 고치지 않는다 (Milestone 3 §6, §21).
/// </summary>
public static class DrawingUnitConversion
{
    /// <summary>Unitless/Other이면 null - "미터로 환산할 수 없다"는 뜻이지 0이 아니다.</summary>
    public static double? MetersPerUnit(DrawingUnit unit) => unit switch
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
