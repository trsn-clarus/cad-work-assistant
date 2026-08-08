namespace CADWorkAssistant.Core.Models;

/// <summary>
/// 사용자가 산출내역을 검토한 상태 (Milestone 7 §9) - 자동 <c>VerificationSeverity</c>와는 별개의
/// 축이다(§8). 자동 검산이 "Review"를 권해도 사용자가 도면을 확인한 뒤 "Verified"로 표시할 수 있고,
/// 그 반대로 자동 검산이 전부 통과해도 사용자가 스스로 "NeedsReview"로 표시해 나중에 다시 볼 항목으로
/// 남겨둘 수 있다. Rejected 같은 승인 Workflow는 추가하지 않는다(§9, 과도한 복잡도 지양).
/// </summary>
public enum QuantityReviewStatus
{
    Unreviewed,
    Verified,
    NeedsReview
}
