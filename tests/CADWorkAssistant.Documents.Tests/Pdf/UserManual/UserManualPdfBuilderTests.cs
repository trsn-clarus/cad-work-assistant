using CADWorkAssistant.Documents.Pdf.UserManual;
using UglyToad.PdfPig;

namespace CADWorkAssistant.Documents.Tests.Pdf.UserManual;

/// <summary>
/// Milestone 13 Part B §118-120 - QuantityPdfBuilderTests와 같은 원칙: 실제 .pdf를 만들고 다시 열어서
/// PdfPig로 렌더링된 텍스트를 검증한다(File.Exists만 확인하지 않는다). 목차의 PageRefField가 실제
/// 페이지 번호로 해석되는지까지 확인한다(§120 - 고정된 가짜 페이지 번호가 아니어야 한다).
/// </summary>
public class UserManualPdfBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _assetsDir;

    public UserManualPdfBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cwa-documents-tests-manual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _assetsDir = Path.Combine(_tempDir, "assets");
        Directory.CreateDirectory(_assetsDir);
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

    private string TargetPath(string fileName) => Path.Combine(_tempDir, fileName);

    private static string ExtractAllText(string path)
    {
        using var document = PdfDocument.Open(path);
        return string.Join("\n", document.GetPages().Select(p => p.Text));
    }

    private static string StripWhitespace(string text) => new(text.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static List<InlineRun> Plain(string text) => new() { new InlineRun(text, InlineStyle.Plain) };

    private static UserManualDocument MinimalTwoChapterDocument() => new()
    {
        Blocks = new List<UserManualBlock>
        {
            new Heading1Block { Text = "CAD Work Assistant" },
            new Heading2Block { Text = "사용설명서" },
            new ParagraphBlock { Runs = Plain("AutoCAD 도면 측정 · 수량 산출 · 검산 · 출력 업무 가이드") },
            new RuleBlock(),
            new Heading1Block { Text = "목차" },
            new NumberedItemBlock { Marker = "1.", Runs = Plain("CAD Work Assistant 소개") },
            new NumberedItemBlock { Marker = "2.", Runs = Plain("설치 및 실행") },
            new RuleBlock(),
            new Heading1Block { Text = "1. CAD Work Assistant 소개" },
            new ParagraphBlock { Runs = Plain("CAD Work Assistant는 AutoCAD로 작업하는 실무자를 위한 프로그램입니다.") },
            new RuleBlock(),
            new Heading1Block { Text = "2. 설치 및 실행" },
            new ParagraphBlock { Runs = Plain("설치 파일을 실행합니다.") },
        },
    };

    // -------------------------------------------------------------
    // Basic validity
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_CreatesValidMultiPagePdf()
    {
        var path = TargetPath("basic.pdf");
        var result = new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "0.8.0", DateTimeOffset.Parse("2026-08-10T12:00:00+09:00"), path);

        Assert.True(File.Exists(path));
        Assert.True(result.PageCount >= 3);
        Assert.Equal(2, result.ChapterCount);

        using var document = PdfDocument.Open(path);
        Assert.Equal(result.PageCount, document.NumberOfPages);
    }

    [Fact]
    public void BuildAndSave_TitleAndSubtitle_AppearOnCoverPage()
    {
        var path = TargetPath("cover.pdf");
        new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "0.8.0", DateTimeOffset.UtcNow, path);

        using var document = PdfDocument.Open(path);
        var coverText = StripWhitespace(document.GetPage(1).Text);
        Assert.Contains(StripWhitespace("CAD Work Assistant"), coverText);
        Assert.Contains(StripWhitespace("사용설명서"), coverText);
    }

    // -------------------------------------------------------------
    // Version string (§115-119)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_VersionString_AppearsOnCoverPage()
    {
        var path = TargetPath("version.pdf");
        new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "1.2.3", DateTimeOffset.UtcNow, path);

        using var document = PdfDocument.Open(path);
        var coverText = StripWhitespace(document.GetPage(1).Text);
        Assert.Contains(StripWhitespace("대상 버전: 1.2.3"), coverText);
    }

    [Fact]
    public void BuildAndSave_VersionIsNotHardcoded_ChangesWithInput()
    {
        var pathA = TargetPath("version-a.pdf");
        var pathB = TargetPath("version-b.pdf");
        new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "0.8.0", DateTimeOffset.UtcNow, pathA);
        new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "9.9.9", DateTimeOffset.UtcNow, pathB);

        Assert.DoesNotContain("9.9.9", ExtractAllText(pathA));
        Assert.Contains("9.9.9", ExtractAllText(pathB));
    }

    // -------------------------------------------------------------
    // Required chapter headings (§118-119)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_ChapterHeadings_AppearAsText()
    {
        var path = TargetPath("headings.pdf");
        new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "0.8.0", DateTimeOffset.UtcNow, path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("1. CAD Work Assistant 소개"), text);
        Assert.Contains(StripWhitespace("2. 설치 및 실행"), text);
    }

    [Fact]
    public void BuildAndSave_KoreanBodyText_RoundTripsExactly()
    {
        var path = TargetPath("korean-body.pdf");
        new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "0.8.0", DateTimeOffset.UtcNow, path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("CAD Work Assistant는 AutoCAD로 작업하는 실무자를 위한 프로그램입니다."), text);
    }

    // -------------------------------------------------------------
    // Real table of contents with resolved page numbers (§120)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_TableOfContents_ResolvesRealPageNumberForChapter()
    {
        // 챕터 2를 여러 페이지 분량의 문단으로 채워 목차/실제 챕터가 서로 다른 페이지에 있게 만든다 -
        // 목차의 페이지 번호가 하드코딩된 값이 아니라 실제로 해석된 값인지 확인하기 위함이다.
        var blocks = new List<UserManualBlock>
        {
            new Heading1Block { Text = "CAD Work Assistant" },
            new Heading2Block { Text = "사용설명서" },
            new ParagraphBlock { Runs = Plain("안내 문서") },
            new RuleBlock(),
            new Heading1Block { Text = "목차" },
            new NumberedItemBlock { Marker = "1.", Runs = Plain("첫 번째 장") },
            new NumberedItemBlock { Marker = "2.", Runs = Plain("두 번째 장") },
            new RuleBlock(),
            new Heading1Block { Text = "1. 첫 번째 장" },
        };
        for (var i = 0; i < 60; i++)
        {
            blocks.Add(new ParagraphBlock { Runs = Plain($"첫 번째 장의 내용을 채우는 문단입니다. 반복 {i}.") });
        }

        blocks.Add(new RuleBlock());
        blocks.Add(new Heading1Block { Text = "2. 두 번째 장" });
        blocks.Add(new ParagraphBlock { Runs = Plain("두 번째 장 내용") });

        var document = new UserManualDocument { Blocks = blocks };
        var path = TargetPath("toc-pageref.pdf");
        var result = new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.UtcNow, path);

        using var pdf = PdfDocument.Open(path);
        Assert.True(result.PageCount > 3, "Chapter 1 should span multiple pages so its TOC entry and heading land on different pages.");

        // "2. 두 번째 장"이 실제로 나타나는 페이지 번호를 찾는다 - 1페이지는 표지, 2페이지는
        // 목차 자체에도 같은 문자열이 나타나므로 3페이지부터 찾는다.
        var chapter2Page = -1;
        for (var pageNumber = 3; pageNumber <= pdf.NumberOfPages; pageNumber++)
        {
            if (StripWhitespace(pdf.GetPage(pageNumber).Text).Contains(StripWhitespace("2. 두 번째 장")))
            {
                chapter2Page = pageNumber;
                break;
            }
        }

        Assert.True(chapter2Page > 2, $"Expected chapter 2 heading on a later page, found on page {chapter2Page}.");

        // 목차 페이지(2페이지)에 그 실제 페이지 번호가 함께 적혀 있어야 한다(고정 값이 아니라 PageRefField로 해석된 값).
        var tocText = StripWhitespace(pdf.GetPage(2).Text);
        Assert.Contains(StripWhitespace("두번째장"), tocText);
        Assert.Contains(chapter2Page.ToString(), tocText);
    }

    // -------------------------------------------------------------
    // Inline formatting / lists / tables / code / images
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_BoldAndCodeInlineRuns_AppearAsPlainText()
    {
        var document = new UserManualDocument
        {
            Blocks = new List<UserManualBlock>
            {
                new Heading1Block { Text = "1. 장" },
                new ParagraphBlock
                {
                    Runs = new List<InlineRun>
                    {
                        new("일반 텍스트 ", InlineStyle.Plain),
                        new("굵은 텍스트", InlineStyle.Bold),
                        new(" 그리고 ", InlineStyle.Plain),
                        new("CADWorkAssistant-Setup.exe", InlineStyle.Code),
                    },
                },
            },
        };

        var path = TargetPath("inline.pdf");
        new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.UtcNow, path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("일반 텍스트 굵은 텍스트 그리고 CADWorkAssistant-Setup.exe"), text);
    }

    [Fact]
    public void BuildAndSave_BulletAndNumberedItems_AppearAsText()
    {
        var document = new UserManualDocument
        {
            Blocks = new List<UserManualBlock>
            {
                new Heading1Block { Text = "1. 장" },
                new BulletItemBlock { Runs = Plain("첫 번째 항목") },
                new BulletItemBlock { Runs = Plain("두 번째 항목") },
                new NumberedItemBlock { Marker = "1.", Runs = Plain("절차 하나") },
                new NumberedItemBlock { Marker = "2.", Runs = Plain("절차 둘") },
            },
        };

        var path = TargetPath("lists.pdf");
        new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.UtcNow, path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("첫 번째 항목"), text);
        Assert.Contains(StripWhitespace("두 번째 항목"), text);
        Assert.Contains(StripWhitespace("절차 하나"), text);
        Assert.Contains(StripWhitespace("절차 둘"), text);
    }

    [Fact]
    public void BuildAndSave_Table_HeadersAndRowsAppearAsText()
    {
        var document = new UserManualDocument
        {
            Blocks = new List<UserManualBlock>
            {
                new Heading1Block { Text = "1. 장" },
                new ManualTableBlock
                {
                    Headers = new[] { "단축키", "기능" },
                    Rows = new List<IReadOnlyList<string>>
                    {
                        new[] { "Ctrl+K", "Command 검색창 열기" },
                        new[] { "Ctrl+L", "Length" },
                    },
                },
            },
        };

        var path = TargetPath("table.pdf");
        new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.UtcNow, path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("단축키"), text);
        Assert.Contains(StripWhitespace("Command 검색창 열기"), text);
        Assert.Contains(StripWhitespace("Ctrl+L"), text);
    }

    [Fact]
    public void BuildAndSave_CodeBlock_PreservesLinesAsText()
    {
        var document = new UserManualDocument
        {
            Blocks = new List<UserManualBlock>
            {
                new Heading1Block { Text = "1. 장" },
                new ManualCodeBlock { Text = "프로젝트 만들기\n      │\n      ▼\nAutoCAD에서 도면 열기" },
            },
        };

        var path = TargetPath("code.pdf");
        new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.UtcNow, path);

        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("프로젝트 만들기"), text);
        Assert.Contains(StripWhitespace("AutoCAD에서 도면 열기"), text);
    }

    [Fact]
    public void BuildAndSave_Image_EmbedsPictureAndCaption()
    {
        var imagePath = Path.Combine(_assetsDir, "sample.png");
        CreateOnePixelPng(imagePath);

        var document = new UserManualDocument
        {
            Blocks = new List<UserManualBlock>
            {
                new Heading1Block { Text = "1. 장" },
                new ManualImageBlock { AltText = "테스트 스크린샷 설명", AbsolutePath = imagePath },
            },
        };

        var path = TargetPath("image.pdf");
        var result = new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.UtcNow, path);

        Assert.True(File.Exists(path));
        var text = StripWhitespace(ExtractAllText(path));
        Assert.Contains(StripWhitespace("테스트 스크린샷 설명"), text);
        Assert.True(result.PageCount >= 1);
    }

    [Fact]
    public void BuildAndSave_MissingImageFile_ThrowsInsteadOfSilentlySkipping()
    {
        var document = new UserManualDocument
        {
            Blocks = new List<UserManualBlock>
            {
                new Heading1Block { Text = "1. 장" },
                new ManualImageBlock { AltText = "없는 스크린샷", AbsolutePath = Path.Combine(_assetsDir, "does-not-exist.png") },
            },
        };

        var path = TargetPath("missing-image.pdf");
        Assert.Throws<FileNotFoundException>(() =>
            new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.UtcNow, path));
    }

    // -------------------------------------------------------------
    // Atomic save (§81-85, QuantityPdfBuilder와 동일 원칙)
    // -------------------------------------------------------------
    [Fact]
    public void BuildAndSave_ExistingFile_ReplacedAtomically()
    {
        var path = TargetPath("overwrite.pdf");
        File.WriteAllText(path, "not a real pdf");

        new UserManualPdfBuilder().BuildAndSave(MinimalTwoChapterDocument(), "0.8.0", DateTimeOffset.UtcNow, path);

        using var document = PdfDocument.Open(path);
        Assert.True(document.NumberOfPages >= 1);
        Assert.DoesNotContain(Directory.GetFiles(_tempDir), f => Path.GetFileName(f).StartsWith("~cwa_", StringComparison.Ordinal));
    }

    private static void CreateOnePixelPng(string path)
    {
        // 최소한의 유효한 1x1 PNG(투명) 바이트를 직접 쓴다 - 테스트에 외부 이미지 자산이 필요 없게 한다.
        var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
        File.WriteAllBytes(path, Convert.FromBase64String(base64));
    }
}
