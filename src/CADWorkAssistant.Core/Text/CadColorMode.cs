namespace CADWorkAssistant.Core.Text;

/// <summary>Milestone 12 §26-30 - AutoCAD 색상 지정 방식. 실제 AutoCAD의
/// Autodesk.AutoCAD.Colors.ColorMethod(ByLayer/ByBlock/ByColor/ByAci/ByPen/...)를 v1에서 지원하는
/// 범위로 좁혔다 - ByPen/Foreground/LayerOff/LayerFrozen 등은 문자 객체에 사용자가 직접 지정할
/// 값이 아니라 조회 전용/특수 상태라 이 앱의 편집 대상에서 제외한다.</summary>
public enum CadColorMode
{
    ByLayer,
    ByBlock,
    Aci,
    TrueColor
}
