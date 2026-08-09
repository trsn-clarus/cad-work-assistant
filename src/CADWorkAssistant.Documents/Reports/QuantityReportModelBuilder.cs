using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;

namespace CADWorkAssistant.Documents.Reports;

/// <summary>
/// Core.Models(QuantityRecord/QuantityReview/Project)와 Core.Verification(QuantityVerificationResult)을
/// <see cref="QuantityReportModel"/>로 옮긴다 - Excel/PDF 렌더러 코드가 QuantityRecord를 직접 읽지
/// 않게 분리한다(Milestone 9 §68-69). 이 클래스 자체는 ClosedXML도 PDFsharp/MigraDoc도 전혀 모른다 -
/// 순수 매핑이라 렌더러 없이도 단위 테스트할 수 있다. Excel/PDF 두 Coordinator가 이 메서드를 동일하게
/// 호출하기 때문에, 같은 Project+Scope라면 두 포맷의 record 순서/필터링 결과가 항상 일치한다
/// (Milestone 10 §144-148, Cross-format consistency).
/// </summary>
public static class QuantityReportModelBuilder
{
    public static QuantityReportModel Build(
        Project project,
        IReadOnlyList<QuantityRecord> records,
        IReadOnlyDictionary<string, QuantityVerificationResult> verifications,
        IReadOnlyDictionary<string, QuantityReview> reviews,
        IQuantityReportOptions options,
        DateTimeOffset generatedAt,
        string appVersion)
    {
        var scoped = options.Scope == QuantityExportScope.VerifiedOnly
            ? records.Where(r => ReviewStatusOf(r, reviews) == QuantityReviewStatus.Verified)
            : records;

        // §142, §148: DB 조회 순서에 흔들리지 않는 결정적 정렬 - CreatedAt 오름차순, 동률이면 Id.
        var ordered = scoped
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        var rows = new List<QuantityReportRow>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var record = ordered[i];
            verifications.TryGetValue(record.Id, out var verification);
            reviews.TryGetValue(record.Id, out var review);

            rows.Add(new QuantityReportRow
            {
                Index = i + 1,
                TypeDisplayName = QuantityTypeDisplay.DisplayName(record.Type),
                Description = string.IsNullOrWhiteSpace(record.Description) ? "-" : record.Description,
                CalculationExpression = record.CalculationExpression,
                Value = record.Value,
                Unit = record.Unit,
                DecimalPlaces = QuantityTypeDisplay.DecimalPlaces(record.Type),
                RawValue = record.RawValue,
                SourceUnit = record.SourceUnit,
                MeasurementSourceDisplay = MeasurementSourceDisplayOf(record.MeasurementSource),
                SourceDrawingFileName = options.IncludeSourceDrawing ? ExtractFileName(record.SourceDrawing) : null,
                ObjectCount = record.ObjectCount,
                CreatedAt = record.CreatedAt,
                VerificationSeverity = verification?.OverallSeverity,
                IsVerificationStale = verification is not null
                    && verification.RuleSetVersion < QuantityVerificationService.CurrentRuleSetVersion,
                VerificationCheckLines = BuildCheckLines(verification),
                ReviewStatus = review?.Status ?? QuantityReviewStatus.Unreviewed,
                ReviewNote = options.IncludeReviewNotes ? review?.Note : null,
            });
        }

        return new QuantityReportModel
        {
            ProjectName = project.Name,
            Client = project.Client,
            Site = project.Site,
            Description = project.Description,
            GeneratedAt = generatedAt,
            AppVersion = appVersion,
            Rows = rows,
        };
    }

    private static QuantityReviewStatus ReviewStatusOf(QuantityRecord record, IReadOnlyDictionary<string, QuantityReview> reviews) =>
        reviews.TryGetValue(record.Id, out var review) ? review.Status : QuantityReviewStatus.Unreviewed;

    /// <summary>QuantityRecord.MeasurementSource는 Core.VerticalArea.MeasurementSourceType.ToString()의
    /// 원본 값이다("CadSelection" 등) - Length/Area는 이 개념 자체가 없어 항상 null이다(§20).</summary>
    private static string MeasurementSourceDisplayOf(string? measurementSource) => measurementSource switch
    {
        "CadSelection" => "CAD에서 선택",
        "ExistingMeasurement" => "기존 측정값 재사용",
        "Manual" => "직접 입력",
        _ => "-"
    };

    private static string? ExtractFileName(string? sourceDrawing) =>
        string.IsNullOrWhiteSpace(sourceDrawing) ? null : Path.GetFileName(sourceDrawing);

    private static IReadOnlyList<string> BuildCheckLines(QuantityVerificationResult? verification)
    {
        if (verification is null || verification.Checks.Count == 0)
        {
            return Array.Empty<string>();
        }

        return verification.Checks
            .Select(c => $"{VerificationSeverityDisplay.Glyph(c.Severity)} {c.Title}")
            .ToList();
    }
}
