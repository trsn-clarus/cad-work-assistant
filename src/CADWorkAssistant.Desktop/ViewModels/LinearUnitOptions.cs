using System.Collections.Generic;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// 사용자가 높이/상부 폭/수동 길이를 입력할 때 고를 수 있는 단위 목록 (Milestone 4 §11, §37) -
/// mm/cm/m 최소 지원. Vertical Area/Parapet 양쪽 ComboBox가 공유한다.
/// </summary>
public static class LinearUnitOptions
{
    public static IReadOnlyList<DrawingUnit> All { get; } = new[]
    {
        DrawingUnit.Millimeters,
        DrawingUnit.Centimeters,
        DrawingUnit.Meters
    };
}
