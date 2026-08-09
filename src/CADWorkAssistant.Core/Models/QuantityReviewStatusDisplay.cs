namespace CADWorkAssistant.Core.Models;

/// <summary>QuantityReviewStatus 표시 문구 - Quantity History UI와 Excel Export가 공유한다
/// (Core.Verification.VerificationSeverityDisplay와 같은 이유, Milestone 9).</summary>
public static class QuantityReviewStatusDisplay
{
    public static string Label(QuantityReviewStatus status) => status switch
    {
        QuantityReviewStatus.Verified => "검토 완료",
        QuantityReviewStatus.NeedsReview => "확인 필요",
        _ => "미검토"
    };
}
