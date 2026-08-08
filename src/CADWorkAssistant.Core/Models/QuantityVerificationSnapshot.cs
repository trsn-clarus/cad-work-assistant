using System;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Core.Models;

/// <summary>
/// <see cref="QuantityVerificationResult"/>(Core.Verification, 방금 계산한 결과)를 DB에 저장하기 위해
/// 직렬화한 형태 (Milestone 7 §14-16). QuantityRecord당 최신 검산 결과 하나만 보존한다 - 매번 다시
/// 검산할 때마다 이전 이력을 쌓지 않는다(upsert-latest-only, §15의 "당시 검산 결과"는 "마지막으로
/// 확인했을 때"로 해석했다 - `docs/QUANTITY_VERIFICATION.md`에 이 범위 결정을 기록한다).
/// </summary>
public sealed class QuantityVerificationSnapshot
{
    public QuantityVerificationSnapshot(
        string id,
        string projectId,
        string quantityRecordId,
        VerificationSeverity overallSeverity,
        int ruleSetVersion,
        DateTimeOffset checkedAt,
        string checksJson)
    {
        Id = id;
        ProjectId = projectId;
        QuantityRecordId = quantityRecordId;
        OverallSeverity = overallSeverity;
        RuleSetVersion = ruleSetVersion;
        CheckedAt = checkedAt;
        ChecksJson = checksJson;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public string QuantityRecordId { get; }

    public VerificationSeverity OverallSeverity { get; }

    /// <summary>이 Snapshot을 만든 규칙 버전 - 현재 엔진의 <c>QuantityVerificationService.
    /// CurrentRuleSetVersion</c>보다 낮으면 "재검산 필요" 상태로 취급한다(§13, §50).</summary>
    public int RuleSetVersion { get; }

    public DateTimeOffset CheckedAt { get; }

    /// <summary>직렬화된 <c>IReadOnlyList&lt;VerificationCheckResult&gt;</c> - 개별 Check마다 별도
    /// child table을 두지 않는다(QuantityRecord.CalculationMetadataJson과 같은 이유, Milestone 6의
    /// "지금 SQL로 쿼리할 필요가 없으면 JSON" 원칙을 그대로 따른다).</summary>
    public string ChecksJson { get; }
}
