using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>프로젝트 전체의 최신 검산/검토 상태 - Quantity History 화면을 열 때 한 번에 불러온다
/// (N+1 방지, §94).</summary>
public sealed class QuantityHistorySnapshotSet
{
    public QuantityHistorySnapshotSet(
        IReadOnlyDictionary<string, QuantityVerificationResult> verifications,
        IReadOnlyDictionary<string, QuantityReview> reviews)
    {
        Verifications = verifications;
        Reviews = reviews;
    }

    /// <summary>Key: QuantityRecordId.</summary>
    public IReadOnlyDictionary<string, QuantityVerificationResult> Verifications { get; }

    /// <summary>Key: QuantityRecordId.</summary>
    public IReadOnlyDictionary<string, QuantityReview> Reviews { get; }
}

public sealed class QuantityVerificationBatchProgress
{
    public QuantityVerificationBatchProgress(int completed, int total)
    {
        Completed = completed;
        Total = total;
    }

    public int Completed { get; }

    public int Total { get; }
}

/// <summary>배치 검산 요약 - 거대한 KPI 카드 4개 대신 한 줄 요약에 쓴다(§70, §157).</summary>
public sealed class QuantityVerificationBatchSummary
{
    public QuantityVerificationBatchSummary(
        int total, int passed, int info, int review, int error, int failed,
        IReadOnlyDictionary<string, QuantityVerificationResult> results)
    {
        Total = total;
        Passed = passed;
        Info = info;
        Review = review;
        Error = error;
        Failed = failed;
        Results = results;
    }

    public int Total { get; }

    public int Passed { get; }

    public int Info { get; }

    public int Review { get; }

    public int Error { get; }

    /// <summary>개별 Check 자체가 아니라 검산 실행 자체가 예외를 던진 레코드 수 - 0이어야 정상이지만
    /// 배치 전체를 죽이지 않기 위한 방어 장치다(§124).</summary>
    public int Failed { get; }

    /// <summary>Key: QuantityRecordId.</summary>
    public IReadOnlyDictionary<string, QuantityVerificationResult> Results { get; }
}

/// <summary>
/// Core.Verification.QuantityVerificationService(순수 계산)를 실제로 실행하고 결과를 Persistence에
/// 저장하는 조립 지점 - ProjectContextService가 Quantity/Activity를 조립하는 것과 같은 역할을
/// Verification/Review에 대해 한다. "빠른 세션"(Project 없음)에서는 계산은 하되 저장은 하지 않는다
/// (ProjectContextService와 같은 원칙).
/// </summary>
public interface IQuantityVerificationCoordinator
{
    Task<QuantityHistorySnapshotSet> LoadForProjectAsync(string? projectId);

    /// <summary>선택한 레코드 하나만 다시 검산한다(§69 "[선택 항목 검산]").</summary>
    Task<QuantityVerificationResult> VerifyAsync(QuantityRecord record, IReadOnlyList<QuantityRecord> allRecordsInScope);

    /// <summary>여러 레코드를 한 번에 검산한다 - Context를 한 번만 만들어 O(n²) 비교를 피하고(§95),
    /// 취소 가능하며(§92), 개별 레코드 실패가 전체를 죽이지 않는다(§124).</summary>
    Task<QuantityVerificationBatchSummary> VerifyBatchAsync(
        IReadOnlyList<QuantityRecord> targets,
        IReadOnlyList<QuantityRecord> allRecordsInScope,
        IProgress<QuantityVerificationBatchProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>logActivity가 true면 "Quantity verified"/"Quantity marked needs review" 활동을 남긴다 -
    /// 사용자가 직접 누른 행동에만 쓴다(§54).</summary>
    Task<QuantityReview> SaveReviewAsync(QuantityRecord record, QuantityReviewStatus status, string? note, bool logActivity);
}
