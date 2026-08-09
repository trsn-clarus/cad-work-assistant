using System;

namespace CADWorkAssistant.Core.Text;

/// <summary>Milestone 12 §10 - 사용자 표시 문구를 한 곳에서 관리한다(Core.Models의 다른 Display 정책과
/// 같은 이유 - UI/Excel/PDF가 같은 문구를 공유해야 할 때 대비).</summary>
public static class CadTextEntityTypeDisplay
{
    public static string Label(CadTextEntityType type) => type switch
    {
        CadTextEntityType.SingleLine => "단일행 문자",
        CadTextEntityType.MultiLine => "여러행 문자",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
