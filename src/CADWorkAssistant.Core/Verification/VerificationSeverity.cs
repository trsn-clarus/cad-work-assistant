namespace CADWorkAssistant.Core.Verification;

/// <summary>
/// 검산 결과의 심각도 (Milestone 7 §7). 숫자 순서가 "더 심각함"을 의미하도록 선언해 여러 Check의
/// 최댓값(<c>Enum.Max</c>)을 그대로 <c>QuantityVerificationResult.OverallSeverity</c>로 쓸 수 있다.
/// Heuristic Check(형상/중복/비교)는 <see cref="Review"/>까지만 쓴다 - 확정적 불일치가 아닌 이상
/// <see cref="Error"/>를 선언하지 않는다(§81, "Heuristic Rule은 자동 Error 금지").
/// </summary>
public enum VerificationSeverity
{
    /// <summary>검산 통과.</summary>
    Pass = 0,

    /// <summary>참고 정보 - 사용자 행동을 요구하지 않는다.</summary>
    Info = 1,

    /// <summary>확인 권장 - Heuristic Check의 최고 심각도.</summary>
    Review = 2,

    /// <summary>결정적 불일치 또는 데이터 오류 - Deterministic Check에서만 나온다.</summary>
    Error = 3
}
