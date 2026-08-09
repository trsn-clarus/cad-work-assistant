namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §9 - 첫 버전은 두 범위만 지원한다. Extents는 저비용으로 추가할 수 있다고
/// 판단했지만(§9) 실제 필요가 확인되지 않아 이번 범위에서 뺐다 - "필요할 때 구현한다"(CLAUDE.md 절대
/// 원칙 6).</summary>
public enum CadPlotScope
{
    /// <summary>현재 Layout의 기존 Page Setup(또는 사용자가 고른 Preset)을 그대로 출력한다 (§10).</summary>
    CurrentLayout,

    /// <summary>Model Space에서 사용자가 지정한 사각 영역만 출력한다 (§11).</summary>
    Window
}
