namespace CADWorkAssistant.Core.Area;

/// <summary>
/// 화면/Inspector에 보여줄 선택 객체 하나. AutoCAD 객체 자체가 아니라 순수 모델이다 (§8).
/// 유효한 것과 제외된 것을 하나의 목록으로 함께 표현한다 - "선택 5개 중 4개만 유효"처럼
/// 전체 맥락(SelectedCount 대비 SupportedCount)을 잃지 않기 위해서다 (§9, §11 PartialSuccess).
/// </summary>
public sealed class AreaMeasurementItem
{
    public AreaMeasurementItem(string handle, string objectType, string layerName, double rawArea, AreaObjectStatus status)
    {
        Handle = handle;
        ObjectType = objectType;
        LayerName = layerName;
        RawArea = rawArea;
        Status = status;
    }

    public string Handle { get; }

    /// <summary>표시용 타입 이름 (예: "Polyline", "Circle", "Region", 또는 미지원 객체의 실제 AutoCAD 타입명).</summary>
    public string ObjectType { get; }

    public string LayerName { get; }

    /// <summary>Status가 Valid가 아니면 0 - 제외된 객체의 면적 값은 의미가 없다.</summary>
    public double RawArea { get; }

    public AreaObjectStatus Status { get; }
}
