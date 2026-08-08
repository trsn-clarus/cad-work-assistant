using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>Database.Insunits(UnitsValue)를 AutoCAD 비의존 DrawingUnit으로 옮긴다. Undefined는 Unitless로,
/// 임의로 mm를 가정하지 않는다 (§19).</summary>
internal static class CadUnitMapper
{
    public static DrawingUnit ToDrawingUnit(UnitsValue units) => units switch
    {
        UnitsValue.Undefined => DrawingUnit.Unitless,
        UnitsValue.Millimeters => DrawingUnit.Millimeters,
        UnitsValue.Centimeters => DrawingUnit.Centimeters,
        UnitsValue.Decimeters => DrawingUnit.Decimeters,
        UnitsValue.Meters => DrawingUnit.Meters,
        UnitsValue.Kilometers => DrawingUnit.Kilometers,
        UnitsValue.Inches => DrawingUnit.Inches,
        UnitsValue.Feet => DrawingUnit.Feet,
        UnitsValue.Yards => DrawingUnit.Yards,
        UnitsValue.Miles => DrawingUnit.Miles,
        _ => DrawingUnit.Other
    };
}
