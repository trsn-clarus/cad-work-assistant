namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// AutoCAD LayerTableRecord를 IPC로 그대로 넘기지 않기 위한 순수 DTO (§39). 이번 Milestone에서는
/// 조회 + On/Off만 지원한다 - Freeze/Thaw는 Drawing 상태에 미치는 영향이 커서(§42) 조회 전용으로
/// 남기고 토글 대상에서 제외했다 (docs/DRAWING_NAVIGATION.md 참고).
/// </summary>
public sealed class CadLayerDto
{
    public CadLayerDto(string name, bool isOn, bool isFrozen, bool isLocked, bool isPlottable, short colorIndex, bool isCurrent)
    {
        Name = name;
        IsOn = isOn;
        IsFrozen = isFrozen;
        IsLocked = isLocked;
        IsPlottable = isPlottable;
        ColorIndex = colorIndex;
        IsCurrent = isCurrent;
    }

    public string Name { get; }

    public bool IsOn { get; }

    public bool IsFrozen { get; }

    public bool IsLocked { get; }

    public bool IsPlottable { get; }

    /// <summary>AutoCAD Color Index(ACI). 실제 색상 렌더링은 이번 Milestone 범위 밖 - 참고 정보로만 노출.</summary>
    public short ColorIndex { get; }

    /// <summary>현재 활성 Layer인지 - 꺼서 작업이 혼란스러워지지 않도록 UI에서 보호 대상으로 표시한다 (§44).</summary>
    public bool IsCurrent { get; }
}
