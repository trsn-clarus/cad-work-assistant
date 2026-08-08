using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Area;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// 면적 산출을 지원하는 Entity인지 판별한다. 리플렉션으로 AutoCAD 2024 acdbmgd.dll을 직접 확인한
/// 결과(Milestone 3 §93-94):
/// - Curve.Area/Curve.Closed는 Curve 기반 클래스 자체에 선언돼 있어 Polyline/Polyline2d/Circle/Ellipse가
///   전부 동일한 방식으로 동작한다. Ellipse는 StartAngle/EndAngle을 가져 호(arc)를 표현할 수 있으므로
///   Closed 검사가 실제로 의미 있다.
/// - Region.Area는 Region에 직접 선언돼 있고, Region은 정의상 항상 닫힌 면이다.
/// Polyline3d는 의도적으로 제외한다 - 비평면 3D 형상의 면적 해석이 불확실하고, 이 개발 PC에는 실제
/// AutoCAD가 없어 실물로 검증할 수 없다 (§15, 확실하지 않으면 Unsupported로 제외). Hatch는 Associative/
/// Pattern/Island 등 복잡도가 높고 마찬가지로 실물 검증이 불가능해 이번 Milestone에서는 제외한다 (§43).
/// </summary>
internal static class CadAreaGeometryMapper
{
    public static SupportedAreaGeometryType? ToSupportedAreaGeometryType(Entity entity) => entity switch
    {
        Polyline => SupportedAreaGeometryType.Polyline,
        Polyline2d => SupportedAreaGeometryType.Polyline,
        Circle => SupportedAreaGeometryType.Circle,
        Ellipse => SupportedAreaGeometryType.Ellipse,
        Region => SupportedAreaGeometryType.Region,
        _ => null
    };
}
