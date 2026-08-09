using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Documents.Pdf;
using CADWorkAssistant.Documents.Reports;
using UglyToad.PdfPig;

namespace CADWorkAssistant.Documents.Tests.Pdf;

/// <summary>
/// 실제로 .pdf를 만들고 다시 열어서 검증한다(§83-88) - File.Exists만 확인하지 않는다. 텍스트 검증은
/// PdfPig(테스트 전용, Apache-2.0, §84)로 페이지를 열어 실제 렌더링된 문자열을 추출해서 한다.
/// PdfPig의 단순 텍스트 추출은 단어 사이 공백을 보존하지 않는 경우가 있어(실제로 겪음), 비교할 때는
/// 양쪽에서 공백을 제거하고 비교한다(<see cref="StripWhitespace"/>).
/// </summary>
public class QuantityPdfBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public QuantityPdfBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cwa-documents-tests-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static Project MakeProject(
        string name = "OO초등학교 옥상방수", string? client = "OO교육청", string? site = "서울 OO구", string? description = null) => new(
        id: "P-1",
        name: name,
        createdAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        updatedAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        lastOpenedAt: DateTimeOffset.Parse("2026-08-09T09:00:00+09:00"),
        client: client,
        site: site,
        description: description);

    private static QuantityRecord MakeRecord(
        string id, string type, decimal value, string unit, DateTimeOffset createdAt,
        decimal? rawValue = null, string? sourceUnit = null, string? calcExpr = null, string? sourceDrawing = null,
        string? description = null) => new(
        id: id,
        type: type,
        layer: "A-ROOF",
        objectCount: 3,
        value: value,
        unit: unit,
        sourceDrawing: sourceDrawing ?? @"C:\Projects\School_Roof.dwg",
        createdAt: createdAt,
        rawValue: rawValue,
        sourceUnit: sourceUnit,
        calculationExpression: calcExpr)
    { Description = description ?? string.Empty };

    private string TargetPath(string fileName) => Path.Combine(_tempDir, fileName);

    private static string ExtractAllText(string path)
    {
        using var document = PdfDocument.Open(path);
        return string.Join("\n", document.GetPages().Select(p => p.Text));
    }

    private static string StripWhitespace(string text) => new(text.Where(c => !char.IsWhiteSpace(c)).ToArray());

    [Fact]
    public void BuildAndSave_CreatesValidMultiPagePdf()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(),
            new[] { MakeRecord("Q-1", "Area", 3102.43m, "m²", DateTimeOffset.Parse("2026-08-01T10:00:00+09:00")) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new PdfExportOptions(),
            DateTimeOffset.Parse("2026-08-09T12:00:00+09:00"),
            "0.9.0");

        var path = TargetPath("basic.pdf");
        var result = new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        Assert.True(File.Exists(path));
        Assert.Equal(1, result.RecordCount);
        Assert.True(result.PageCount >= 1);

        using var document = PdfDocument.Open(path);
        Assert.Equal(result.PageCount, document.NumberOfPages);
    }

    [Fact]
    public void BuildAndSave_ProjectName_AppearsInDocument()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(name: "서울의료원 옥상 방수공사"),
            new[] { MakeRecord("Q-1", "Area", 100m, "m²", DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new PdfExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        var path = TargetPath("project-name.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("서울의료원 옥상 방수공사"), text);
    }

    // -------------------------------------------------------------
    // Regression fixtures (§91) - the exact master-prompt values must appear as text.
    // -------------------------------------------------------------
    [Theory]
    [InlineData("Length", 255.940660, "m")]
    [InlineData("Area", 3102.43, "m²")]
    [InlineData("VerticalArea", 25.594066, "m²")]
    [InlineData("VerticalArea", 29.5141237, "m²")]
    [InlineData("Parapet", 69.0537, "m²")]
    public void BuildAndSave_RegressionValue_AppearsWithCorrectDisplayPrecision(string type, double rawDouble, string unit)
    {
        var value = (decimal)rawDouble;
        var model = QuantityReportModelBuilder.Build(
            MakeProject(),
            new[] { MakeRecord("Q-1", type, value, unit, DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new PdfExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        var expectedDisplay = value.ToString("N" + model.Rows[0].DecimalPlaces, System.Globalization.CultureInfo.InvariantCulture);

        var path = TargetPath($"regression-{type}-{Guid.NewGuid():N}.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace(expectedDisplay), text);
    }

    // -------------------------------------------------------------
    // Verification / Review text (§93-94, §131-132 텍스트가 항상 함께 있어야 한다)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_VerificationSeverities_AppearAsTextLabels()
    {
        var now = DateTimeOffset.UtcNow;
        var records = new[]
        {
            MakeRecord("Q-pass", "Area", 100m, "m²", now),
            MakeRecord("Q-review", "Area", 200m, "m²", now.AddMinutes(1)),
            MakeRecord("Q-error", "Area", 300m, "m²", now.AddMinutes(2)),
        };
        var verifications = new Dictionary<string, QuantityVerificationResult>
        {
            ["Q-pass"] = new("Q-pass", 1, now, new[] { new VerificationCheckResult("R", VerificationSeverity.Pass, "정상", "ok") }),
            ["Q-review"] = new("Q-review", 1, now, new[] { new VerificationCheckResult("R", VerificationSeverity.Review, "중복 의심", "msg") }),
            ["Q-error"] = new("Q-error", 1, now, new[] { new VerificationCheckResult("R", VerificationSeverity.Error, "단위 불일치", "msg") }),
        };

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), records, verifications, new Dictionary<string, QuantityReview>(),
            new PdfExportOptions(), now, "0.9.0");

        var path = TargetPath("verification-severities.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("검산 완료"), text);
        Assert.Contains(StripWhitespace("확인 필요"), text);
        Assert.Contains(StripWhitespace("오류"), text);

        // §131: 실제로 렌더링해서 발견한 문제 - 맑은 고딕에 ✓(U+2713) 글리프가 없어 PDF에서 빈
        // 사각형(tofu)으로 깨졌다. PDF 전용으로 ○(U+25CB, 실제 렌더링 확인됨)로 치환했는지 확인한다 -
        // ✓ 문자 자체가 출력물에 남아있으면 안 된다(다시 tofu로 깨진다는 뜻이다).
        Assert.Contains(StripWhitespace("○ 검산 완료"), text);
        Assert.DoesNotContain('✓', ExtractAllText(path));
    }

    [Fact]
    public void BuildAndSave_KoreanReviewNote_RoundTripsExactly()
    {
        var now = DateTimeOffset.UtcNow;
        var record = MakeRecord("Q-1", "Area", 100m, "m²", now);
        var reviews = new Dictionary<string, QuantityReview>
        {
            ["Q-1"] = new("R-1", "P-1", "Q-1", QuantityReviewStatus.Verified, "현장 확인 완료. ㄱ자 평면으로 정상.", now),
        };

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record }, new Dictionary<string, QuantityVerificationResult>(), reviews,
            new PdfExportOptions(), now, "0.9.0");

        var path = TargetPath("review-note-unicode.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("현장 확인 완료. ㄱ자 평면으로 정상."), text);
    }

    [Fact]
    public void BuildAndSave_VerifiedWithAutomaticError_KeepsErrorVisibleNotHidden()
    {
        // §26: 자동 검산 결과와 사용자 검토 상태는 독립된 축 - 둘 다 그대로 보여준다.
        var now = DateTimeOffset.UtcNow;
        var record = MakeRecord("Q-1", "Area", 100m, "m²", now);
        var verifications = new Dictionary<string, QuantityVerificationResult>
        {
            ["Q-1"] = new("Q-1", 1, now, new[] { new VerificationCheckResult("R", VerificationSeverity.Error, "수량이 0 이하입니다", "msg") }),
        };
        var reviews = new Dictionary<string, QuantityReview>
        {
            ["Q-1"] = new("R-1", "P-1", "Q-1", QuantityReviewStatus.Verified, null, now),
        };

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record }, verifications, reviews, new PdfExportOptions(), now, "0.9.0");

        var path = TargetPath("verified-with-error.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("오류"), text);
        Assert.Contains(StripWhitespace("검토 완료"), text);
    }

    // -------------------------------------------------------------
    // Null-safety (§127)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_AllOptionalProjectFieldsNull_DoesNotCrash()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(client: null, site: null, description: null),
            new[] { MakeRecord("Q-1", "Length", 10m, "m", DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new PdfExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        var path = TargetPath("null-fields.pdf");
        var result = new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        Assert.True(File.Exists(path));
        Assert.Equal(1, result.RecordCount);
    }

    [Fact]
    public void BuildAndSave_RecordWithoutCalculationExpression_ShowsFallbackNotCrash()
    {
        var record = MakeRecord("Q-1", "Length", 10m, "m", DateTimeOffset.UtcNow, calcExpr: null);
        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record }, new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(), new PdfExportOptions(), DateTimeOffset.UtcNow, "0.9.0");

        var path = TargetPath("no-formula.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("세부 입력정보 없음"), text);
    }

    [Fact]
    public void BuildAndSave_LongReviewNote_DoesNotCrashAndAppearsInFull()
    {
        var longNote = string.Join(" ", Enumerable.Repeat("현장 재확인이 필요한 항목으로 다음 방문 시 재실측 예정.", 20));
        var now = DateTimeOffset.UtcNow;
        var record = MakeRecord("Q-1", "Area", 100m, "m²", now);
        var reviews = new Dictionary<string, QuantityReview>
        {
            ["Q-1"] = new("R-1", "P-1", "Q-1", QuantityReviewStatus.NeedsReview, longNote, now),
        };

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record }, new Dictionary<string, QuantityVerificationResult>(), reviews,
            new PdfExportOptions(), now, "0.9.0");

        var path = TargetPath("long-review-note.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("현장 재확인이 필요한 항목으로 다음 방문 시 재실측 예정."), text);
    }

    // -------------------------------------------------------------
    // Export scope (§32-34, §140)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_VerifiedOnlyScope_TitleReflectsScope()
    {
        var now = DateTimeOffset.UtcNow;
        var record = MakeRecord("Q-1", "Area", 100m, "m²", now);
        var reviews = new Dictionary<string, QuantityReview>
        {
            ["Q-1"] = new("R-1", "P-1", "Q-1", QuantityReviewStatus.Verified, null, now),
        };

        var options = new PdfExportOptions { Scope = QuantityExportScope.VerifiedOnly };
        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record }, new Dictionary<string, QuantityVerificationResult>(), reviews,
            options, now, "0.9.0");

        var path = TargetPath("verified-only-title.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, options, path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("검토 완료 수량 보고서"), text);
    }

    // -------------------------------------------------------------
    // Empty / large exports (§65-66, §123-126)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_ZeroRecords_StillProducesValidPdf()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(), Array.Empty<QuantityRecord>(),
            new Dictionary<string, QuantityVerificationResult>(), new Dictionary<string, QuantityReview>(),
            new PdfExportOptions(), DateTimeOffset.UtcNow, "0.9.0");

        var path = TargetPath("empty.pdf");
        var result = new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        Assert.Equal(0, result.RecordCount);
        Assert.True(result.PageCount >= 1);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("내보낼 수량이 없습니다."), text);
    }

    [Fact]
    public void BuildAndSave_LargeExport_1000Records_CompletesWithinBudgetAndReopens()
    {
        var now = DateTimeOffset.Parse("2026-08-01T00:00:00+09:00");
        var records = new List<QuantityRecord>(1_000);
        for (var i = 0; i < 1_000; i++)
        {
            records.Add(MakeRecord($"Q-{i}", "Area", 100m + i, "m²", now.AddSeconds(i)));
        }

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), records, new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(), new PdfExportOptions(), now, "0.9.0");

        var path = TargetPath("large-1000.pdf");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);
        sw.Stop();

        Assert.Equal(1_000, result.RecordCount);
        Assert.True(result.PageCount > 1);
        using var document = PdfDocument.Open(path);
        Assert.Equal(result.PageCount, document.NumberOfPages);

        // §66, §123-126: 정확한 임계값을 못박지 않는다 - PDF는 페이지 단위 텍스트 레이아웃이라
        // Excel(셀 쓰기)보다 훨씬 느릴 수 있다는 것을 실제로 감안해 넉넉하게 잡는다.
        Assert.True(sw.Elapsed.TotalSeconds < 120, $"Export of 1,000 records took {sw.Elapsed.TotalSeconds:N1}s");
    }

    // -------------------------------------------------------------
    // Atomic save (§81-85)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_ExistingFile_ReplacedAtomically()
    {
        var path = TargetPath("overwrite.pdf");
        File.WriteAllText(path, "not a real pdf");

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { MakeRecord("Q-1", "Area", 100m, "m²", DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(), new Dictionary<string, QuantityReview>(),
            new PdfExportOptions(), DateTimeOffset.UtcNow, "0.9.0");

        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), path);

        using var document = PdfDocument.Open(path);
        Assert.True(document.NumberOfPages >= 1);

        Assert.DoesNotContain(Directory.GetFiles(_tempDir), f => Path.GetFileName(f).StartsWith("~cwa_", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildAndSave_SourceDrawing_NeverIncludesFullPath()
    {
        var now = DateTimeOffset.UtcNow;
        var record = MakeRecord("Q-1", "Area", 100m, "m²", now, sourceDrawing: @"C:\Sensitive\Company\Path\School_Roof.dwg");

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record }, new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(), new PdfExportOptions { IncludeSourceDrawing = true },
            now, "0.9.0");

        Assert.Equal("School_Roof.dwg", model.Rows[0].SourceDrawingFileName);

        var path = TargetPath("source-drawing.pdf");
        new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions { IncludeSourceDrawing = true }, path);

        var text = ExtractAllText(path);
        Assert.DoesNotContain(@"C:\Sensitive", text);
        Assert.Contains(StripWhitespace("School_Roof.dwg"), StripWhitespace(text));
    }
}
