using System;

namespace CADWorkAssistant.Core.Text;

/// <summary>Milestone 12 §22 - Text Height는 0보다 커야 한다. NaN/Infinity도 막는다(사용자 입력
/// 파싱 실패를 그대로 통과시키지 않는다).</summary>
public static class TextHeightValidator
{
    public static bool IsValid(double height) => height > 0 && !double.IsNaN(height) && !double.IsInfinity(height);
}
