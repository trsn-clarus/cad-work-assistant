using System;
using System.Collections.Generic;
using System.Linq;

namespace CADWorkAssistant.Core.Verification;

/// <summary>
/// 하나의 QuantityRecord에 대한 전체 검산 결과 (Milestone 7 §14). Persistence의
/// <c>QuantityVerificationSnapshot</c>(Core.Models)와는 다른 타입이다 - 이건 방금 계산한 결과이고,
/// Snapshot은 그걸 DB에 저장하기 위해 직렬화한 형태다(Length의 LengthMeasurementResult/QuantityRecord
/// 관계와 같은 패턴).
/// </summary>
public sealed class QuantityVerificationResult
{
    public QuantityVerificationResult(
        string quantityRecordId,
        int ruleSetVersion,
        DateTimeOffset checkedAt,
        IReadOnlyList<VerificationCheckResult> checks)
    {
        QuantityRecordId = quantityRecordId;
        RuleSetVersion = ruleSetVersion;
        CheckedAt = checkedAt;
        Checks = checks;
        OverallSeverity = checks.Count == 0
            ? VerificationSeverity.Pass
            : checks.Select(c => c.Severity).Max();
    }

    public string QuantityRecordId { get; }

    public VerificationSeverity OverallSeverity { get; }

    public int RuleSetVersion { get; }

    public DateTimeOffset CheckedAt { get; }

    public IReadOnlyList<VerificationCheckResult> Checks { get; }
}
