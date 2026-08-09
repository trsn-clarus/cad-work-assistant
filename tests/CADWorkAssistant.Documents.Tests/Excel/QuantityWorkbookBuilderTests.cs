using ClosedXML.Excel;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Documents.Excel;
using CADWorkAssistant.Documents.Reports;

namespace CADWorkAssistant.Documents.Tests.Excel;

/// <summary>
/// 실제로 .xlsx를 만들고 다시 열어서 검증한다(§88) - File.Exists만 확인하지 않는다. 테스트가 만든
/// 파일은 항상 임시 폴더에만 쓰고 Repository에 commit하지 않는다(§149).
/// </summary>
public class QuantityWorkbookBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public QuantityWorkbookBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cwa-documents-tests-" + Guid.NewGuid().ToString("N"));
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

    private static Project MakeProject(string name = "OO초등학교 옥상방수", string? client = "OO교육청", string? site = "서울 OO구") => new(
        id: "P-1",
        name: name,
        createdAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        updatedAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        lastOpenedAt: DateTimeOffset.Parse("2026-08-09T09:00:00+09:00"),
        client: client,
        site: site);

    private static QuantityRecord MakeRecord(
        string id, string type, decimal value, string unit, DateTimeOffset createdAt,
        decimal? rawValue = null, string? sourceUnit = null, string? calcExpr = null, string? sourceDrawing = null) => new(
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
        calculationExpression: calcExpr);

    private string TargetPath(string fileName) => Path.Combine(_tempDir, fileName);

    [Fact]
    public void BuildAndSave_CreatesFileWithFourSheets()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(),
            new[] { MakeRecord("Q-1", "Area", 3102.43m, "m²", DateTimeOffset.Parse("2026-08-01T10:00:00+09:00")) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(),
            DateTimeOffset.Parse("2026-08-09T12:00:00+09:00"),
            "0.9.0");

        var path = TargetPath("basic.xlsx");
        var result = new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        Assert.True(File.Exists(path));
        Assert.Equal(1, result.RecordCount);

        using var workbook = new XLWorkbook(path);
        Assert.Equal(new[] { "수량산출서", "산출근거", "검산내역", "프로젝트정보" }, workbook.Worksheets.Select(w => w.Name));
    }

    [Fact]
    public void BuildAndSave_OptionalSheetsOff_OmitsThem()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(),
            new[] { MakeRecord("Q-1", "Area", 100m, "m²", DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        var options = new ExcelExportOptions { IncludeCalculationBasis = false, IncludeVerificationDetail = false };
        var path = TargetPath("no-optional-sheets.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, options, path);

        using var workbook = new XLWorkbook(path);
        Assert.Equal(new[] { "수량산출서", "프로젝트정보" }, workbook.Worksheets.Select(w => w.Name));
    }

    [Fact]
    public void BuildAndSave_ProjectName_WrittenToProjectInfoSheet()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(name: "서울의료원 옥상 방수공사"),
            new[] { MakeRecord("Q-1", "Area", 100m, "m²", DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        var path = TargetPath("project-name.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        var infoSheet = workbook.Worksheet("프로젝트정보");
        var found = infoSheet.CellsUsed().Any(c => c.GetString() == "서울의료원 옥상 방수공사");
        Assert.True(found, "Project name should appear verbatim in the 프로젝트정보 sheet.");
    }

    // -------------------------------------------------------------
    // Regression fixtures (§91) - numeric cell type + value, reopened.
    // -------------------------------------------------------------
    [Theory]
    [InlineData("Length", 255.940660, "m", 3)]
    [InlineData("Area", 3102.43, "m²", 2)]
    [InlineData("VerticalArea", 25.594066, "m²", 3)]
    [InlineData("VerticalArea", 29.5141237, "m²", 3)]
    [InlineData("Parapet", 69.0537, "m²", 3)]
    public void BuildAndSave_RegressionValue_StoredAsNumericCell(string type, double rawDouble, string unit, int expectedDecimalPlaces)
    {
        var value = (decimal)rawDouble;
        var model = QuantityReportModelBuilder.Build(
            MakeProject(),
            new[] { MakeRecord("Q-1", type, value, unit, DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(),
            DateTimeOffset.UtcNow, "0.9.0");

        Assert.Equal(expectedDecimalPlaces, model.Rows[0].DecimalPlaces);

        var path = TargetPath($"regression-{type}-{Guid.NewGuid():N}.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("수량산출서");
        var quantityCell = sheet.CellsUsed()
            .First(c => c.DataType == XLDataType.Number && c.GetDouble() == (double)value);

        Assert.Equal(XLDataType.Number, quantityCell.DataType);
        Assert.Contains("0.", quantityCell.Style.NumberFormat.Format);
    }

    // -------------------------------------------------------------
    // Verification / Review text (§93-94)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_VerificationSeverities_WrittenAsGlyphAndText()
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
            new ExcelExportOptions(), now, "0.9.0");

        var path = TargetPath("verification-severities.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("수량산출서");
        var allText = string.Join(" ", sheet.CellsUsed().Select(c => c.GetString()));

        Assert.Contains("✓ 검산 완료", allText);
        Assert.Contains("! 확인 필요", allText);
        Assert.Contains("× 오류", allText);
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
            new ExcelExportOptions(), now, "0.9.0");

        var path = TargetPath("review-note-unicode.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("검산내역");
        var found = sheet.CellsUsed().Any(c => c.GetString() == "현장 확인 완료. ㄱ자 평면으로 정상.");
        Assert.True(found);
    }

    // -------------------------------------------------------------
    // Security (§153-155)
    // -------------------------------------------------------------
    [Theory]
    [InlineData("=CMD('/c calc')!A1")]
    [InlineData("+SUM(1+1)")]
    [InlineData("-2+3")]
    [InlineData("@SUM(1)")]
    public void BuildAndSave_FormulaLikeUserText_StoredAsLiteralTextNotFormula(string dangerousText)
    {
        var now = DateTimeOffset.UtcNow;
        var record = MakeRecord("Q-1", "Area", 100m, "m²", now);
        record.Description = dangerousText;

        var model = QuantityReportModelBuilder.Build(
            MakeProject(name: dangerousText), new[] { record },
            new Dictionary<string, QuantityVerificationResult>(), new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(), now, "0.9.0");

        var path = TargetPath($"formula-injection-{Guid.NewGuid():N}.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("수량산출서");
        var cell = sheet.CellsUsed().First(c => c.GetString() == dangerousText);

        // 핵심 검증: 이 cell은 Formula가 아니라 순수 텍스트여야 한다 - HasFormula가 true면
        // Excel이 실제로 이 문자열을 수식으로 재해석할 위험이 있다는 뜻이다.
        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);
    }

    [Fact]
    public void BuildAndSave_SourceDrawing_NeverWrittenAsHyperlink()
    {
        var now = DateTimeOffset.UtcNow;
        var record = MakeRecord("Q-1", "Area", 100m, "m²", now, sourceDrawing: @"C:\Sensitive\Company\Path\School_Roof.dwg");

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { record }, new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(), new ExcelExportOptions { IncludeSourceDrawing = true },
            now, "0.9.0");

        // §23: 전체 경로가 아니라 파일명만.
        Assert.Equal("School_Roof.dwg", model.Rows[0].SourceDrawingFileName);

        var path = TargetPath("source-drawing.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("산출근거");
        Assert.Empty(sheet.Hyperlinks);
        Assert.DoesNotContain(sheet.CellsUsed(), c => c.GetString().Contains(@"C:\Sensitive"));
    }

    // -------------------------------------------------------------
    // Empty / large exports
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_ZeroRecords_StillProducesValidWorkbook()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(), Array.Empty<QuantityRecord>(),
            new Dictionary<string, QuantityVerificationResult>(), new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(), DateTimeOffset.UtcNow, "0.9.0");

        var path = TargetPath("empty.xlsx");
        var result = new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        Assert.Equal(0, result.RecordCount);
        using var workbook = new XLWorkbook(path);
        Assert.NotEmpty(workbook.Worksheets);
    }

    [Fact]
    public void BuildAndSave_LargeExport_10000Records_CompletesAndReopens()
    {
        var now = DateTimeOffset.Parse("2026-08-01T00:00:00+09:00");
        var records = new List<QuantityRecord>(10_000);
        for (var i = 0; i < 10_000; i++)
        {
            records.Add(MakeRecord($"Q-{i}", "Area", 100m + i, "m²", now.AddSeconds(i)));
        }

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), records, new Dictionary<string, QuantityVerificationResult>(),
            new Dictionary<string, QuantityReview>(), new ExcelExportOptions(), now, "0.9.0");

        var path = TargetPath("large-10000.xlsx");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);
        sw.Stop();

        Assert.Equal(10_000, result.RecordCount);
        Assert.True(File.Exists(path));

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("수량산출서");
        Assert.True(sheet.LastRowUsed()!.RowNumber() > 10_000);

        // 정확한 임계값을 못박지 않는다(§99) - 합리적인 시간 안에 끝난다는 것만 확인한다(수 분이 아니라 수십 초 이내).
        Assert.True(sw.Elapsed.TotalSeconds < 60, $"Export of 10,000 records took {sw.Elapsed.TotalSeconds:N1}s");
    }

    // -------------------------------------------------------------
    // Atomic save (§84-87)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_ExistingFile_ReplacedAtomically()
    {
        var path = TargetPath("overwrite.xlsx");
        File.WriteAllText(path, "not a real xlsx");

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { MakeRecord("Q-1", "Area", 100m, "m²", DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(), new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(), DateTimeOffset.UtcNow, "0.9.0");

        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        Assert.NotEmpty(workbook.Worksheets);

        // No leftover temp files in the directory.
        Assert.DoesNotContain(Directory.GetFiles(_tempDir), f => Path.GetFileName(f).StartsWith("~cwa_", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildAndSave_PrintSetup_IsLandscapeA4FitToOnePageWide()
    {
        var model = QuantityReportModelBuilder.Build(
            MakeProject(), new[] { MakeRecord("Q-1", "Area", 100m, "m²", DateTimeOffset.UtcNow) },
            new Dictionary<string, QuantityVerificationResult>(), new Dictionary<string, QuantityReview>(),
            new ExcelExportOptions(), DateTimeOffset.UtcNow, "0.9.0");

        var path = TargetPath("print-setup.xlsx");
        new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), path);

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet("수량산출서");
        Assert.Equal(XLPageOrientation.Landscape, sheet.PageSetup.PageOrientation);
        Assert.Equal(XLPaperSize.A4Paper, sheet.PageSetup.PaperSize);
        Assert.Equal(1, sheet.PageSetup.PagesWide);
    }
}
