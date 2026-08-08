namespace CADWorkAssistant.Core.Area;

/// <summary>
/// Milestone 3에서 면적 산출을 지원하는 Geometry 종류. 전부 AutoCAD 2024 Managed API에서
/// `Curve.Area`/`Curve.Closed`(Polyline 계열, Circle, Ellipse) 또는 `Region.Area`로
/// 안전하게 읽을 수 있음을 리플렉션으로 확인했다 (Milestone 3 §13-15, §93-94).
/// Polyline2d는 Polyline과 구분 없이 Polyline으로 합쳐 보여준다 - Length의 §14 관례를 그대로 따른다.
/// </summary>
public enum SupportedAreaGeometryType
{
    Polyline,
    Circle,
    Ellipse,
    Region
}
