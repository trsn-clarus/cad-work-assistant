namespace CADWorkAssistant.Core.Models;

/// <summary>Milestone 13 §41 - Project Overview에 보여줄 수량 요약("전체 42건 · 검토 완료 35건 ·
/// 확인 필요 5건 · 오류 1건"). QuantityHistory 화면을 다시 만들지 않고, Review/Verification 두
/// 축을 집계만 해서 압축한 결과다 - 가짜 KPI 카드가 아니라 실제 QuantityRecord/QuantityReview/
/// QuantityVerificationSnapshot을 읽어 만든다.</summary>
public sealed class ProjectQuantitySummary
{
    public ProjectQuantitySummary(int total, int verifiedCount, int needsReviewCount, int errorCount)
    {
        Total = total;
        VerifiedCount = verifiedCount;
        NeedsReviewCount = needsReviewCount;
        ErrorCount = errorCount;
    }

    public int Total { get; }

    /// <summary>QuantityReviewStatus.Verified(사용자가 "검토 완료"로 표시한 건).</summary>
    public int VerifiedCount { get; }

    /// <summary>QuantityReviewStatus.NeedsReview(사용자가 "확인 필요"로 표시한 건).</summary>
    public int NeedsReviewCount { get; }

    /// <summary>VerificationSeverity.Error가 하나라도 있는 QuantityVerificationSnapshot의 건수
    /// (자동 검산 기준 - 검토 상태와 별개 축, §89-91의 "자동 검산 vs 사용자 검토" 구분을 그대로
    /// 따른다).</summary>
    public int ErrorCount { get; }
}
