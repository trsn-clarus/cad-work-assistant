using System.Collections.Generic;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// IsolateObjects/SetLayerVisibility가 임시로 바꾼 표시 상태를 기억해뒀다가 RestoreVisibility가
/// 정확히 되돌릴 수 있게 한다 (Milestone 5 §45-47). Plugin 하나에 인스턴스 하나 - Extension이
/// 생성해서 세 Handler(Isolate/SetLayerVisibility/RestoreVisibility)에 공유한다.
///
/// "모든 Layer를 On으로 되돌리는" 잘못된 복원(§46)을 피하려면 반드시 *변경 직전* 상태를 남겨야
/// 한다 - 그래서 스냅샷은 매 호출마다 새로 찍지 않고, 활성 Isolation이 없을 때 딱 한 번만 찍는다.
/// </summary>
internal sealed class DrawingIsolationState
{
    /// <summary>Object Isolation으로 CWA가 숨긴 Handle들 - Restore는 이 목록만 다시 보이게 한다.
    /// null이면 활성 Object Isolation이 없다.</summary>
    public HashSet<string>? HiddenObjectHandles { get; set; }

    /// <summary>Layer Isolation/토글을 시작하기 *직전*의 전체 Layer On/Off 상태 (Layer 이름 → 원래
    /// On 여부). null이면 활성 Layer 변경이 없다.</summary>
    public Dictionary<string, bool>? OriginalLayerOnState { get; set; }

    public bool HasActiveIsolation => HiddenObjectHandles is not null || OriginalLayerOnState is not null;

    public void Clear()
    {
        HiddenObjectHandles = null;
        OriginalLayerOnState = null;
    }
}
