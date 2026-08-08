using System;

namespace CADWorkAssistant.Core.Models;

/// <summary>
/// QuantityRecord 하나에 대한 사용자 검토 상태 (Milestone 7 §9-10). QuantityRecord당 최신 상태 하나만
/// 의미가 있다(이력이 아니다) - Project의 RecentMeasurement/DrawingFile과 같은 upsert-latest-only
/// 패턴(`docs/PERSISTENCE.md` §3)을 그대로 따른다.
/// </summary>
public sealed class QuantityReview
{
    public QuantityReview(
        string id,
        string projectId,
        string quantityRecordId,
        QuantityReviewStatus status,
        string? note,
        DateTimeOffset? reviewedAt)
    {
        Id = id;
        ProjectId = projectId;
        QuantityRecordId = quantityRecordId;
        Status = status;
        Note = note;
        ReviewedAt = reviewedAt;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public string QuantityRecordId { get; }

    public QuantityReviewStatus Status { get; set; }

    /// <summary>"ㄱ자 평면이라 둘레가 길지만 정상" 같은 사용자 메모(§10, §66) - 자동 검산 결과를
    /// 지우지 않는다, 둘 다 나란히 보여준다.</summary>
    public string? Note { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
