using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Documents.Excel;
using CADWorkAssistant.Documents.Reports;

namespace CADWorkAssistant.Documents.Tests.Reports;

public class QuantityReportModelBuilderTests
{
    private static Project MakeProject() => new(
        id: "P-1",
        name: "OO초등학교 옥상방수",
        createdAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        updatedAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        lastOpenedAt: DateTimeOffset.Parse("2026-08-09T09:00:00+09:00"),
        client: "OO교육청",
        site: "서울 OO구");

    private static QuantityRecord MakeRecord(string id, string type, decimal value, DateTimeOffset createdAt) => new(
        id: id,
        type: type,
        layer: "A-ROOF",
        objectCount: 3,
        value: value,
        unit: type == "Length" ? "m" : "m²",
        sourceDrawing: @"C:\Projects\School_Roof.dwg",
        createdAt: createdAt);

    [Fact]
    public void Build_AllScope_IncludesEveryRecord()
    {
        var project = MakeProject();
        var records = new[]
        {
            MakeRecord("Q-1", "Area", 3102.43m, DateTimeOffset.Parse("2026-08-01T10:00:00+09:00")),
            MakeRecord("Q-2", "VerticalArea", 25.594066m, DateTimeOffset.Parse("2026-08-01T10:05:00+09:00")),
        };

        var model = QuantityReportModelBuilder.Build(
            project, records,
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions { Scope = QuantityExportScope.All },
            DateTimeOffset.Parse("2026-08-09T12:00:00+09:00"),
            "0.9.0");

        Assert.Equal(2, model.TotalCount);
        Assert.Equal("OO초등학교 옥상방수", model.ProjectName);
        Assert.Equal(1, model.Rows[0].Index);
        Assert.Equal(2, model.Rows[1].Index);
    }

    [Fact]
    public void Build_VerifiedOnlyScope_ExcludesUnreviewedAndNeedsReview()
    {
        var project = MakeProject();
        var now = DateTimeOffset.Parse("2026-08-01T10:00:00+09:00");
        var records = new[]
        {
            MakeRecord("Q-1", "Area", 100m, now),
            MakeRecord("Q-2", "Area", 200m, now.AddMinutes(1)),
            MakeRecord("Q-3", "Area", 300m, now.AddMinutes(2)),
            MakeRecord("Q-4", "Area", 400m, now.AddMinutes(3)),
        };
        var reviews = new Dictionary<string, QuantityReview>
        {
            ["Q-1"] = new("R-1", "P-1", "Q-1", QuantityReviewStatus.Verified, null, now),
            ["Q-2"] = new("R-2", "P-1", "Q-2", QuantityReviewStatus.NeedsReview, null, now),
            // Q-3: Unreviewed (no entry)
            ["Q-4"] = new("R-4", "P-1", "Q-4", QuantityReviewStatus.Verified, null, now),
        };

        var model = QuantityReportModelBuilder.Build(
            project, records,
            new Dictionary<string, QuantityVerificationResult>(),
            reviews,
            new ExcelExportOptions { Scope = QuantityExportScope.VerifiedOnly },
            now,
            "0.9.0");

        Assert.Equal(2, model.TotalCount);
        Assert.All(model.Rows, r => Assert.True(r.ReviewStatus == QuantityReviewStatus.Verified));
    }

    [Fact]
    public void Build_VerifiedRecordWithVerificationError_KeepsErrorVisible()
    {
        // §136-137: 자동 검산 Error라도 사용자가 Verified로 표시했으면 Verified-only 범위에 포함하고,
        // Error 상태 자체는 숨기지 않는다.
        var project = MakeProject();
        var now = DateTimeOffset.Parse("2026-08-01T10:00:00+09:00");
        var record = MakeRecord("Q-1", "Area", 100m, now);
        var reviews = new Dictionary<string, QuantityReview>
        {
            ["Q-1"] = new("R-1", "P-1", "Q-1", QuantityReviewStatus.Verified, "확인함", now),
        };
        var verifications = new Dictionary<string, QuantityVerificationResult>
        {
            ["Q-1"] = new("Q-1", 1, now, new[]
            {
                new VerificationCheckResult("PositiveQuantity", VerificationSeverity.Error, "수량이 0 이하입니다", "message"),
            }),
        };

        var model = QuantityReportModelBuilder.Build(
            project, new[] { record }, verifications, reviews,
            new ExcelExportOptions { Scope = QuantityExportScope.VerifiedOnly },
            now, "0.9.0");

        Assert.Single(model.Rows);
        Assert.Equal(VerificationSeverity.Error, model.Rows[0].VerificationSeverity);
        Assert.Equal(QuantityReviewStatus.Verified, model.Rows[0].ReviewStatus);
        Assert.Equal(1, model.VerificationErrorCount);
        Assert.Equal(1, model.VerifiedCount);
    }

    [Fact]
    public void Build_NoRecords_ReturnsEmptyModel()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(), Array.Empty<QuantityRecord>(),
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        Assert.Equal(0, model.TotalCount);
        Assert.Empty(model.Rows);
    }

    [Fact]
    public void Build_SourceDrawingOptionOff_HidesFileName()
    {
        var record = MakeRecord("Q-1", "Length", 10m, DateTimeOffset.UtcNow);

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions { IncludeSourceDrawing = false },
            DateTimeOffset.UtcNow, "0.9.0");

        Assert.Null(model.Rows[0].SourceDrawingFileName);
    }

    [Fact]
    public void Build_SourceDrawingOptionOn_ExtractsFileNameOnly()
    {
        var record = MakeRecord("Q-1", "Length", 10m, DateTimeOffset.UtcNow);

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions { IncludeSourceDrawing = true },
            DateTimeOffset.UtcNow, "0.9.0");

        Assert.Equal("School_Roof.dwg", model.Rows[0].SourceDrawingFileName);
    }

    [Fact]
    public void Build_DeterministicOrdering_SortsByCreatedAtThenId()
    {
        var now = DateTimeOffset.Parse("2026-08-01T10:00:00+09:00");
        // 일부러 뒤죽박죽 순서로 넘긴다 - 결과는 항상 CreatedAt 오름차순이어야 한다(§142, §148).
        var records = new[]
        {
            MakeRecord("Q-3", "Length", 3m, now.AddMinutes(2)),
            MakeRecord("Q-1", "Length", 1m, now),
            MakeRecord("Q-2", "Length", 2m, now.AddMinutes(1)),
        };

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), records,
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(),
            now, "0.9.0");

        Assert.Equal(new[] { 1m, 2m, 3m }, model.Rows.Select(r => r.Value));
    }

    [Theory]
    [InlineData("Length", 3)]
    [InlineData("Area", 2)]
    [InlineData("VerticalArea", 3)]
    [InlineData("Parapet", 3)]
    public void Build_DecimalPlaces_MatchesTypePolicy(string type, int expectedDecimalPlaces)
    {
        var record = MakeRecord("Q-1", type, 1.23456m, DateTimeOffset.UtcNow);

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        Assert.Equal(expectedDecimalPlaces, model.Rows[0].DecimalPlaces);
    }
}
