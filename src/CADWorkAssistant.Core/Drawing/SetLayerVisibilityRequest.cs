using System.Collections.Generic;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>Layer 하나를 어떤 상태로 만들지 - 개별 토글과 "선택 Layer만 보기"(여러 개를 한 번에) 둘 다
/// 이 하나의 요청 목록으로 표현한다 (§37, §41, §11 - 기능마다 별도 명령을 만들지 않는다).</summary>
public sealed class LayerVisibilityChange
{
    public LayerVisibilityChange(string layerName, bool isOn)
    {
        LayerName = layerName;
        IsOn = isOn;
    }

    public string LayerName { get; }

    public bool IsOn { get; }
}

/// <summary>
/// `SetLayerVisibility` IPC 요청 payload. Handler는 첫 변경 전에 전체 Layer 상태를 스냅샷으로
/// 남겨두고, 이후 몇 번을 호출하든 `RestoreVisibility`가 스냅샷 시점 상태로 정확히 되돌린다 (§45-46).
/// </summary>
public sealed class SetLayerVisibilityRequest
{
    public SetLayerVisibilityRequest(IReadOnlyList<LayerVisibilityChange> changes)
    {
        Changes = changes;
    }

    public IReadOnlyList<LayerVisibilityChange> Changes { get; }
}
