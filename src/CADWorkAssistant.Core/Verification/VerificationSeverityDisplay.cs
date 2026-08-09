namespace CADWorkAssistant.Core.Verification;

/// <summary>
/// VerificationSeverity(또는 검산 자체가 없음, null)를 사람이 읽는 Glyph/Label로 바꾼다. 원래
/// Desktop.ViewModels.QuantityHistoryRow에 인라인으로만 있던 switch를 Core로 옮겼다(Milestone 9) -
/// Quantity History UI와 Excel Export(향후 PDF도) 양쪽이 "검산 완료"/"확인 필요" 같은 문구가 서로
/// 갈라지지 않게 정책을 한 곳에서만 정의한다. 색상만으로 상태를 전달하지 않는다 - Glyph와 Label을
/// 항상 같이 노출한다(Milestone 7 §58, §113).
/// </summary>
public static class VerificationSeverityDisplay
{
    public static string Glyph(VerificationSeverity? severity) => severity switch
    {
        null => "—",
        VerificationSeverity.Pass => "✓",
        VerificationSeverity.Review => "!",
        VerificationSeverity.Error => "×",
        _ => "?"
    };

    public static string Label(VerificationSeverity? severity) => severity switch
    {
        null => "미검산",
        VerificationSeverity.Pass => "검산 완료",
        VerificationSeverity.Review => "확인 필요",
        VerificationSeverity.Error => "오류",
        _ => "검산 불가"
    };
}
