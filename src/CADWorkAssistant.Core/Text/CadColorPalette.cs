using System.Collections.Generic;

namespace CADWorkAssistant.Core.Text;

/// <summary>
/// Milestone 12 §26-29 - v1에서 지원하는 compact 색상 선택지. 256색 전체 ACI picker를 처음부터
/// 만들지 않는다(§28) - ByLayer/ByBlock + 자주 쓰는 ACI 7가지로 시작한다. 색상 7은 배경에 따라
/// White/Black으로 달리 보일 수 있어 이름에 그 사실을 그대로 반영한다(§29) - "White"라고 단정하지
/// 않는다.
/// </summary>
public static class CadColorPalette
{
    public static CadColorDto ByLayer { get; } = new(CadColorMode.ByLayer, 0, 0, 0, 0, "ByLayer");

    public static CadColorDto ByBlock { get; } = new(CadColorMode.ByBlock, 0, 0, 0, 0, "ByBlock");

    public static IReadOnlyList<CadColorDto> CommonAci { get; } = new[]
    {
        Aci(1, "빨강 (색상 1)"),
        Aci(2, "노랑 (색상 2)"),
        Aci(3, "초록 (색상 3)"),
        Aci(4, "청록 (색상 4)"),
        Aci(5, "파랑 (색상 5)"),
        Aci(6, "자홍 (색상 6)"),
        Aci(7, "색상 7 · White/Black")
    };

    private static CadColorDto Aci(short index, string displayName) =>
        new(CadColorMode.Aci, index, 0, 0, 0, displayName);
}
