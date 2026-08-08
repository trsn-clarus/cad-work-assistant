using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Persistence;
using Serilog;

namespace CADWorkAssistant.Desktop.Services;

public sealed class QuantityVerificationCoordinator : IQuantityVerificationCoordinator
{
    private readonly ProjectDataService _dataService;

    public QuantityVerificationCoordinator(ProjectDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<QuantityHistorySnapshotSet> LoadForProjectAsync(string? projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            // 빠른 세션 - 저장된 검산/검토가 있을 수 없다.
            return new QuantityHistorySnapshotSet(
                new Dictionary<string, QuantityVerificationResult>(),
                new Dictionary<string, QuantityReview>());
        }

        using var connection = _dataService.Database.OpenConnection();
        var snapshots = await _dataService.QuantityVerifications.GetByProjectAsync(projectId, connection);
        var reviews = await _dataService.QuantityReviews.GetByProjectAsync(projectId, connection);

        var verifications = snapshots.ToDictionary(s => s.QuantityRecordId, ToResult);
        var reviewMap = reviews.ToDictionary(r => r.QuantityRecordId);

        return new QuantityHistorySnapshotSet(verifications, reviewMap);
    }

    public async Task<QuantityVerificationResult> VerifyAsync(QuantityRecord record, IReadOnlyList<QuantityRecord> allRecordsInScope)
    {
        var context = QuantityVerificationContext.Build(allRecordsInScope);
        var result = QuantityVerificationService.Verify(record, context, DateTimeOffset.UtcNow);

        if (!string.IsNullOrEmpty(record.ProjectId))
        {
            await _dataService.SaveVerificationBatchAsync(new[] { ToSnapshot(result, record.ProjectId) });
        }

        return result;
    }

    public async Task<QuantityVerificationBatchSummary> VerifyBatchAsync(
        IReadOnlyList<QuantityRecord> targets,
        IReadOnlyList<QuantityRecord> allRecordsInScope,
        IProgress<QuantityVerificationBatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var context = QuantityVerificationContext.Build(allRecordsInScope);
        var results = new Dictionary<string, QuantityVerificationResult>();
        var snapshotsToSave = new List<QuantityVerificationSnapshot>();
        var checkedAt = DateTimeOffset.UtcNow;
        int passed = 0, info = 0, review = 0, error = 0, failed = 0;

        for (var i = 0; i < targets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = targets[i];

            try
            {
                var result = QuantityVerificationService.Verify(record, context, checkedAt);
                results[record.Id] = result;

                switch (result.OverallSeverity)
                {
                    case VerificationSeverity.Pass: passed++; break;
                    case VerificationSeverity.Info: info++; break;
                    case VerificationSeverity.Review: review++; break;
                    case VerificationSeverity.Error: error++; break;
                }

                if (!string.IsNullOrEmpty(record.ProjectId))
                {
                    snapshotsToSave.Add(ToSnapshot(result, record.ProjectId));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // §124: 레코드 하나의 검산 실패가 배치 전체를 죽이지 않는다.
                failed++;
                Log.Warning(ex, "Quantity verification failed for record {RecordId}", record.Id);
            }

            progress?.Report(new QuantityVerificationBatchProgress(i + 1, targets.Count));
        }

        if (snapshotsToSave.Count > 0)
        {
            await _dataService.SaveVerificationBatchAsync(snapshotsToSave);
        }

        return new QuantityVerificationBatchSummary(targets.Count, passed, info, review, error, failed, results);
    }

    public async Task<QuantityReview> SaveReviewAsync(QuantityRecord record, QuantityReviewStatus status, string? note, bool logActivity)
    {
        var reviewedAt = DateTimeOffset.UtcNow;
        var review = new QuantityReview(Guid.NewGuid().ToString("N"), record.ProjectId, record.Id, status, note, reviewedAt);

        if (string.IsNullOrEmpty(record.ProjectId))
        {
            // 빠른 세션 - 저장할 Project가 없다. 호출부(ViewModel)가 화면에는 반영하되 DB에는 남기지 않는다.
            return review;
        }

        ActivityRecord? activity = null;
        if (logActivity)
        {
            var title = status switch
            {
                QuantityReviewStatus.Verified => "산출내역 검토 완료",
                QuantityReviewStatus.NeedsReview => "산출내역 확인 필요로 표시",
                _ => "산출내역 검토 상태 변경"
            };
            activity = new ActivityRecord(Guid.NewGuid().ToString("N"), record.ProjectId,
                status == QuantityReviewStatus.Verified ? "QuantityVerified" : "QuantityMarkedForReview",
                title, $"{record.Type} · {record.Layer}", reviewedAt);
        }

        await _dataService.SaveReviewAsync(review, activity);
        return review;
    }

    private static QuantityVerificationSnapshot ToSnapshot(QuantityVerificationResult result, string projectId) =>
        new(Guid.NewGuid().ToString("N"), projectId, result.QuantityRecordId, result.OverallSeverity,
            result.RuleSetVersion, result.CheckedAt, JsonSerializer.Serialize(result.Checks, IpcJson.Options));

    private static QuantityVerificationResult ToResult(QuantityVerificationSnapshot snapshot)
    {
        var checks = JsonSerializer.Deserialize<List<VerificationCheckResult>>(snapshot.ChecksJson, IpcJson.Options)
            ?? new List<VerificationCheckResult>();
        return new QuantityVerificationResult(snapshot.QuantityRecordId, snapshot.RuleSetVersion, snapshot.CheckedAt, checks);
    }
}
