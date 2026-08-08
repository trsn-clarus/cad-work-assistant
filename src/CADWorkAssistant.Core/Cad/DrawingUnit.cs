namespace CADWorkAssistant.Core.Cad;

/// <summary>
/// AutoCAD Database.Insunits(UnitsValue)를 매핑한 결과. Unitless는 실제로 그렇게 표시해야 하며,
/// mm로 임의 추측하지 않는다 (§19). AutoCAD가 지원하는 단위 중 자주 쓰이지 않는 것은 Other로 묶는다.
/// </summary>
public enum DrawingUnit
{
    Unitless,
    Millimeters,
    Centimeters,
    Decimeters,
    Meters,
    Kilometers,
    Inches,
    Feet,
    Yards,
    Miles,
    Other
}
