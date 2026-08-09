namespace CADWorkAssistant.Core.Text;

/// <summary>Milestone 12 §10 - 지원하는 두 문자 객체 종류. AutoCAD의 DBText(단일행)/MText(여러행)에
/// 각각 대응한다. 줄바꿈이 있다고 자동으로 MultiLine으로 바꾸는 magic behavior는 하지 않는다(§38) -
/// 사용자가 Create 시 명시적으로 고른다.</summary>
public enum CadTextEntityType
{
    SingleLine,
    MultiLine
}
