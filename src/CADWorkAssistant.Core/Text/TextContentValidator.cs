namespace CADWorkAssistant.Core.Text;

/// <summary>Milestone 12 §97-99 - 사용자가 입력한 문자열을 임의로 trim/대문자화하지 않는다(§97) -
/// 이 검증기는 존재 여부만 판단한다. 빈 문자열/공백만 있는 내용은 Create/Edit 둘 다 금지한다(§98-99).</summary>
public static class TextContentValidator
{
    public static bool IsValid(string? content) => !string.IsNullOrWhiteSpace(content);
}
