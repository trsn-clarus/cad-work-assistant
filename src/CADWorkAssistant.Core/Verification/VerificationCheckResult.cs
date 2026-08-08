namespace CADWorkAssistant.Core.Verification;

/// <summary>
/// 검산 규칙 하나의 결과 (Milestone 7 §14). <see cref="TechnicalDetails"/>는 개발자/숙련 사용자를 위한
/// 계산 근거(원본값/기대값 등)이고, 기본 Inspector 화면에는 노출하지 않는다 - "[자세히]"에서만 보여준다
/// (§64).
/// </summary>
public sealed class VerificationCheckResult
{
    public VerificationCheckResult(
        string ruleId,
        VerificationSeverity severity,
        string title,
        string message,
        string? technicalDetails = null)
    {
        RuleId = ruleId;
        Severity = severity;
        Title = title;
        Message = message;
        TechnicalDetails = technicalDetails;
    }

    /// <summary>예: "FiniteValue", "UnitConsistency" - Ruleset 문서/테스트가 이 문자열로 규칙을 가리킨다.</summary>
    public string RuleId { get; }

    public VerificationSeverity Severity { get; }

    /// <summary>짧은 요약 (예: "단위 정상", "저장값과 원본 단위 변환 결과가 일치하지 않습니다").</summary>
    public string Title { get; }

    /// <summary>사용자가 이해할 수 있는 설명 - "왜 표시되었는지"가 항상 드러나야 한다(§153).</summary>
    public string Message { get; }

    public string? TechnicalDetails { get; }
}
