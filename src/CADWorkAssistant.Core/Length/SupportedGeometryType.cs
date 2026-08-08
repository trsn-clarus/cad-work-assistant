namespace CADWorkAssistant.Core.Length;

/// <summary>
/// Milestone 2에서 길이 산출을 지원하는 Geometry 종류. AutoCAD의 Polyline/Polyline2d/Polyline3d는
/// 전부 여기서 Polyline 하나로 합쳐 보여준다 - 사용자에게는 구분할 의미가 없다 (§14).
/// </summary>
public enum SupportedGeometryType
{
    Line,
    Polyline,
    Arc
}
