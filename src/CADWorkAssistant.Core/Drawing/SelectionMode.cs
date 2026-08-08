namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// 영역 선택 방식 (§26-27). Window는 영역 내부에 완전히 들어온 객체만, Crossing은 영역에 닿기만 해도
/// 포함한다 - AutoCAD의 표준 SelectWindow/SelectCrossingWindow 의미를 그대로 따른다.
/// </summary>
public enum SelectionMode
{
    Window,
    Crossing
}
