namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// AutoCAD DBObject를 IPC로 그대로 넘기지 않기 위한 순수 DTO (§13-14). Length/Area와 달리 이 기능은
/// "계산 가능한 지원 타입"으로 좁히지 않고 선택된 모든 객체를 대상으로 한다 - ObjectType은 제한된
/// Enum이 아니라 AutoCAD의 실제 타입 이름 그대로(예: "Polyline", "MText", "BlockReference")다.
/// </summary>
public sealed class CadSelectedObjectDto
{
    public CadSelectedObjectDto(string handle, string objectType, string layerName, CadBoundsDto? bounds)
    {
        Handle = handle;
        ObjectType = objectType;
        LayerName = layerName;
        Bounds = bounds;
    }

    public string Handle { get; }

    public string ObjectType { get; }

    public string LayerName { get; }

    /// <summary>일부 객체(예: 빈 Block)는 유효한 Extents가 없을 수 있어 nullable이다.</summary>
    public CadBoundsDto? Bounds { get; }
}
