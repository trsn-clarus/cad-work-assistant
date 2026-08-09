using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Documents.Excel;
using CADWorkAssistant.Documents.Pdf;
using CADWorkAssistant.Documents.Reports;
using ClosedXML.Excel;
using UglyToad.PdfPig;

namespace CADWorkAssistant.Documents.Tests.Reports;

/// <summary>
/// Milestone 10 §144-148 - Excel과 PDF는 같은 <see cref="QuantityReportModel"/> 인스턴스를 그대로
/// 받아 렌더링하므로(둘 다 QuantityReportModelBuilder.Build를 한 번만 호출한 결과를 공유), 같은
/// Project+Scope에 대해 record 순서/개수/표시값/검산·검토 텍스트가 항상 일치해야 한다. 이 테스트는
/// 그 보장을 실제 출력 파일 두 개를 만들어서 확인한다 - 모델을 공유한다는 사실만으로는 렌더러 쪽
/// 버그(예: PDF가 순서를 뒤집어 그린다)를 잡을 수 없기 때문이다.
/// </summary>
public class CrossFormatConsistencyTests : IDisposable
{
    private readonly string _tempDir;

    public CrossFormatConsistencyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cwa-documents-tests-crossformat-" + Guid.NewGuid().ToString("N"));
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

    private static Project MakeProject() => new(
        id: "P-1",
        name: "서울의료원 옥상 방수공사",
        createdAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        updatedAt: DateTimeOffset.Parse("2026-08-01T09:00:00+09:00"),
        lastOpenedAt: DateTimeOffset.Parse("2026-08-09T09:00:00+09:00"),
        client: "서울의료원",
        site: "서울");

    private static QuantityRecord MakeRecord(string id, string type, decimal value, string unit, DateTimeOffset createdAt, string description) => new(
        id: id,
        type: type,
        layer: "A-ROOF",
        objectCount: 3,
        value: value,
        unit: unit,
        sourceDrawing: @"C:\Projects\School_Roof.dwg",
        createdAt: createdAt)
    { Description = description };

    [Fact]
    public void SameModel_RenderedAsExcelAndPdf_ProducesSameRecordCountAndOrder()
    {
        var now = DateTimeOffset.Parse("2026-08-01T10:00:00+09:00");
        // 일부러 뒤죽박죽 CreatedAt로 넘긴다 - QuantityReportModelBuilder가 항상 CreatedAt 오름차순으로
        // 정렬하므로(§142, §148) 두 렌더러 모두 "파라펫" -> "루프 드레인" -> "옥상 바닥" 순서로 나와야 한다.
        var records = new[]
        {
            MakeRecord("Q-3", "Area", 12.5m, "m²", now.AddMinutes(20), "옥상 바닥"),
            MakeRecord("Q-1", "Parapet", 69.054m, "m²", now, "파라펫 내·외측 및 상부면"),
            MakeRecord("Q-2", "Length", 3.2m, "m", now.AddMinutes(10), "루프 드레인 둘레"),
        };
        var verifications = new Dictionary<string, QuantityVerificationResult>
        {
            ["Q-1"] = new("Q-1", 1, now, new[] { new VerificationCheckResult("R", VerificationSeverity.Pass, "정상", "ok") }),
        };
        var reviews = new Dictionary<string, QuantityReview>
        {
            ["Q-2"] = new("R-2", "P-1", "Q-2", QuantityReviewStatus.Verified, "현장 대조 완료.", now),
        };

        var model = QuantityReportModelBuilder.Build(
            MakeProject(), records, verifications, reviews,
            new ExcelExportOptions(), now, "0.9.0");

        var excelPath = Path.Combine(_tempDir, "cross.xlsx");
        var excelResult = new QuantityWorkbookBuilder().BuildAndSave(model, new ExcelExportOptions(), excelPath);

        var pdfPath = Path.Combine(_tempDir, "cross.pdf");
        var pdfResult = new QuantityPdfBuilder().BuildAndSave(model, new PdfExportOptions(), pdfPath);

        // 1) Record count 동일 (§147)
        Assert.Equal(excelResult.RecordCount, pdfResult.RecordCount);
        Assert.Equal(3, excelResult.RecordCount);

        // 2) Order 동일 - 두 출력물 모두 "파라펫" -> "루프 드레인" -> "옥상 바닥" 순서로 나타나야 한다.
        using var workbook = new XLWorkbook(excelPath);
        var excelSheet = workbook.Worksheet("수량산출서");
        var excelDescriptionsInOrder = excelSheet.CellsUsed()
            .Where(c => c.Address.ColumnNumber == 3 && c.Address.RowNumber > 1)
            .OrderBy(c => c.Address.RowNumber)
            .Select(c => c.GetString())
            .Where(s => s is "파라펫 내·외측 및 상부면" or "루프 드레인 둘레" or "옥상 바닥")
            .ToList();
        Assert.Equal(new[] { "파라펫 내·외측 및 상부면", "루프 드레인 둘레", "옥상 바닥" }, excelDescriptionsInOrder);

        using var pdfDocument = PdfDocument.Open(pdfPath);
        var pdfText = string.Join("\n", pdfDocument.GetPages().Select(p => p.Text));
        var pdfTextNoSpace = StripWhitespace(pdfText);
        var indexParapet = pdfTextNoSpace.IndexOf(StripWhitespace("파라펫 내·외측 및 상부면"), StringComparison.Ordinal);
        var indexDrain = pdfTextNoSpace.IndexOf(StripWhitespace("루프 드레인 둘레"), StringComparison.Ordinal);
        var indexRoof = pdfTextNoSpace.IndexOf(StripWhitespace("옥상 바닥"), StringComparison.Ordinal);

        Assert.True(indexParapet >= 0 && indexDrain >= 0 && indexRoof >= 0, "All three descriptions must appear in the PDF.");
        Assert.True(indexParapet < indexDrain, "파라펫 must appear before 루프 드레인 in the PDF (CreatedAt order).");
        Assert.True(indexDrain < indexRoof, "루프 드레인 must appear before 옥상 바닥 in the PDF (CreatedAt order).");

        // 3) 표시 수량 텍스트 동일 (§147, "display quantities 동일")
        Assert.Contains(StripWhitespace("69.054"), pdfTextNoSpace);
        Assert.Contains(excelSheet.CellsUsed(), c => c.DataType == XLDataType.Number && c.GetDouble() == 69.054);

        // 4) 검산/검토 텍스트 동일 (§147). 텍스트 라벨("검산 완료")은 Excel/PDF 둘 다 완전히 같다.
        // 글리프만 다르다 - Excel은 뷰어 폰트가 ✓(U+2713)를 그대로 보여주지만, PDF는 실제로 렌더링해
        // 확인해보니 맑은 고딕에 그 글리프가 없어(§131) ○(U+25CB)로 대체해서 보여준다
        // (QuantityPdfBuilder.ToPdfSafeGlyph) - 렌더러마다 자기 폰트가 지원하는 안전한 표현을 쓰되
        // 텍스트 라벨은 절대 갈라지지 않는다는 게 핵심 보장이다.
        Assert.Contains(StripWhitespace("검산 완료"), pdfTextNoSpace);
        Assert.Contains(StripWhitespace("○ 검산 완료"), pdfTextNoSpace);
        var excelAllText = string.Join(" ", excelSheet.CellsUsed().Select(c => c.GetString()));
        Assert.Contains("✓ 검산 완료", excelAllText);

        Assert.Contains(StripWhitespace("검토 완료"), pdfTextNoSpace);
        Assert.Contains("검토 완료", excelAllText);
    }

    private static string StripWhitespace(string text) => new(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
