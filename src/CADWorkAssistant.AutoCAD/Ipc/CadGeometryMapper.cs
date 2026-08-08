using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Length;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// 길이 산출을 지원하는 Entity인지 판별한다. Polyline/Polyline2d/Polyline3d는 전부 Curve 파생이며
/// (리플렉션으로 확인함, Milestone 2 §14-15) 사용자에게는 구분 없이 "Polyline"으로 보여준다.
/// 지원하지 않는 타입은 null - 호출부가 "제외 목록"에 담는다 (§18).
/// </summary>
internal static class CadGeometryMapper
{
    public static SupportedGeometryType? ToSupportedGeometryType(Entity entity) => entity switch
    {
        Line => SupportedGeometryType.Line,
        Arc => SupportedGeometryType.Arc,
        Polyline => SupportedGeometryType.Polyline,
        Polyline2d => SupportedGeometryType.Polyline,
        Polyline3d => SupportedGeometryType.Polyline,
        _ => null
    };
}
