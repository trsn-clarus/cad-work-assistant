using CADWorkAssistant.Documents.Pdf.UserManual;
using CADWorkAssistant.ManualBuilder;
using UglyToad.PdfPig;

namespace CADWorkAssistant.ManualBuilder.Tests;

public class ManualMarkdownParserTests : IDisposable
{
    private readonly string _tempDir;

    public ManualMarkdownParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cwa-manualbuilder-tests-" + Guid.NewGuid().ToString("N"));
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

    private static string RunText(IReadOnlyList<InlineRun> runs) => string.Concat(runs.Select(r => r.Text));

    [Fact]
    public void Parse_Headings_ProducesHeading1AndHeading2Blocks()
    {
        var document = ManualMarkdownParser.Parse("# 제목\n## 부제목\n", _tempDir);

        Assert.Collection(document.Blocks,
            b => Assert.Equal("제목", Assert.IsType<Heading1Block>(b).Text),
            b => Assert.Equal("부제목", Assert.IsType<Heading2Block>(b).Text));
    }

    [Fact]
    public void Parse_HorizontalRule_ProducesRuleBlock()
    {
        var document = ManualMarkdownParser.Parse("# A\n\n---\n\n# B\n", _tempDir);

        Assert.IsType<RuleBlock>(document.Blocks[1]);
    }

    [Fact]
    public void Parse_ParagraphWithWrappedLines_JoinsIntoSingleParagraph()
    {
        var markdown = "이것은 첫 줄이고\n두 번째 줄로 이어집니다.\n";
        var document = ManualMarkdownParser.Parse(markdown, _tempDir);

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        Assert.Equal("이것은 첫 줄이고 두 번째 줄로 이어집니다.", RunText(paragraph.Runs));
    }

    [Fact]
    public void Parse_BulletListWithContinuationLine_JoinsAndStopsAtNextBullet()
    {
        var markdown = "- 첫 항목 시작하고\n  이어지는 설명입니다.\n- 두 번째 항목\n";
        var document = ManualMarkdownParser.Parse(markdown, _tempDir);

        Assert.Equal(2, document.Blocks.Count);
        var first = Assert.IsType<BulletItemBlock>(document.Blocks[0]);
        Assert.Equal("첫 항목 시작하고 이어지는 설명입니다.", RunText(first.Runs));
        var second = Assert.IsType<BulletItemBlock>(document.Blocks[1]);
        Assert.Equal("두 번째 항목", RunText(second.Runs));
    }

    [Fact]
    public void Parse_NumberedList_PreservesOriginalMarkerPerItem()
    {
        var markdown = "1. 첫 단계\n2. 둘째 단계\n10. 열번째 단계\n";
        var document = ManualMarkdownParser.Parse(markdown, _tempDir);

        Assert.Collection(document.Blocks,
            b => Assert.Equal("1.", Assert.IsType<NumberedItemBlock>(b).Marker),
            b => Assert.Equal("2.", Assert.IsType<NumberedItemBlock>(b).Marker),
            b => Assert.Equal("10.", Assert.IsType<NumberedItemBlock>(b).Marker));
    }

    [Fact]
    public void Parse_BoldAndCodeInline_ProducesSeparateStyledRuns()
    {
        var markdown = "일반 **굵게** 그리고 `code` 끝\n";
        var document = ManualMarkdownParser.Parse(markdown, _tempDir);

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        Assert.Collection(paragraph.Runs,
            r => Assert.Equal(("일반 ", InlineStyle.Plain), (r.Text, r.Style)),
            r => Assert.Equal(("굵게", InlineStyle.Bold), (r.Text, r.Style)),
            r => Assert.Equal((" 그리고 ", InlineStyle.Plain), (r.Text, r.Style)),
            r => Assert.Equal(("code", InlineStyle.Code), (r.Text, r.Style)),
            r => Assert.Equal((" 끝", InlineStyle.Plain), (r.Text, r.Style)));
    }

    [Fact]
    public void Parse_Image_ResolvesPathRelativeToBaseDirectory()
    {
        var document = ManualMarkdownParser.Parse("![화면 설명](assets/01-test.png)\n", _tempDir);

        var image = Assert.IsType<ManualImageBlock>(Assert.Single(document.Blocks));
        Assert.Equal("화면 설명", image.AltText);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "assets", "01-test.png")), image.AbsolutePath);
    }

    [Fact]
    public void Parse_CodeFence_PreservesLinesVerbatim()
    {
        var markdown = "```\n프로젝트 만들기\n      │\n      ▼\n도면 열기\n```\n";
        var document = ManualMarkdownParser.Parse(markdown, _tempDir);

        var code = Assert.IsType<ManualCodeBlock>(Assert.Single(document.Blocks));
        Assert.Equal("프로젝트 만들기\n      │\n      ▼\n도면 열기", code.Text);
    }

    [Fact]
    public void Parse_PipeTable_ParsesHeaderAndRowsSkippingSeparator()
    {
        var markdown = "| 단축키 | 기능 |\n|---|---|\n| Ctrl+K | 검색 |\n| Ctrl+L | Length |\n";
        var document = ManualMarkdownParser.Parse(markdown, _tempDir);

        var table = Assert.IsType<ManualTableBlock>(Assert.Single(document.Blocks));
        Assert.Equal(new[] { "단축키", "기능" }, table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "Ctrl+K", "검색" }, table.Rows[0]);
        Assert.Equal(new[] { "Ctrl+L", "Length" }, table.Rows[1]);
    }

    [Fact]
    public void Parse_TableMissingSeparatorRow_ThrowsFormatException()
    {
        var markdown = "| 단축키 | 기능 |\n| Ctrl+K | 검색 |\n";
        Assert.Throws<FormatException>(() => ManualMarkdownParser.Parse(markdown, _tempDir));
    }

    // -------------------------------------------------------------
    // Real document regression (docs/user-guide/ko-KR/USER_GUIDE.md) - §139, §159
    // -------------------------------------------------------------
    [Fact]
    public void Parse_RealUserGuide_ProducesExactly24NumberedChapters()
    {
        var repoRoot = FindRepoRoot();
        var markdownPath = Path.Combine(repoRoot, "docs", "user-guide", "ko-KR", "USER_GUIDE.md");
        var baseDirectory = Path.GetDirectoryName(markdownPath)!;

        var document = ManualMarkdownParser.Parse(File.ReadAllText(markdownPath), baseDirectory);

        var chapterHeadings = document.Blocks
            .OfType<Heading1Block>()
            .Where(h => System.Text.RegularExpressions.Regex.IsMatch(h.Text, @"^\d+\.\s"))
            .ToList();

        Assert.Equal(24, chapterHeadings.Count);
    }

    [Fact]
    public void Parse_RealUserGuide_AllReferencedScreenshotsExistOnDisk()
    {
        var repoRoot = FindRepoRoot();
        var markdownPath = Path.Combine(repoRoot, "docs", "user-guide", "ko-KR", "USER_GUIDE.md");
        var baseDirectory = Path.GetDirectoryName(markdownPath)!;

        var document = ManualMarkdownParser.Parse(File.ReadAllText(markdownPath), baseDirectory);

        var images = document.Blocks.OfType<ManualImageBlock>().ToList();
        Assert.NotEmpty(images);
        foreach (var image in images)
        {
            Assert.True(File.Exists(image.AbsolutePath), $"Referenced screenshot missing: {image.AbsolutePath}");
        }
    }

    // -------------------------------------------------------------
    // Full pipeline against the real document (§118-127) - not just synthetic fixtures.
    // -------------------------------------------------------------
    [Fact]
    public void RealUserGuide_BuiltIntoPdf_ContainsAllChapterTitlesAndNoTofuGlyph()
    {
        var repoRoot = FindRepoRoot();
        var markdownPath = Path.Combine(repoRoot, "docs", "user-guide", "ko-KR", "USER_GUIDE.md");
        var baseDirectory = Path.GetDirectoryName(markdownPath)!;

        var document = ManualMarkdownParser.Parse(File.ReadAllText(markdownPath), baseDirectory);
        var pdfPath = Path.Combine(_tempDir, "real-user-guide.pdf");
        var result = new UserManualPdfBuilder().BuildAndSave(document, "0.8.0", DateTimeOffset.Parse("2026-08-10T00:00:00+09:00"), pdfPath);

        Assert.Equal(24, result.ChapterCount);
        Assert.True(result.PageCount > 20, $"Expected a substantial multi-page manual, got {result.PageCount} pages.");

        using var pdf = PdfDocument.Open(pdfPath);
        var fullText = string.Concat(pdf.GetPages().Select(p => p.Text).SelectMany(t => t.Where(c => !char.IsWhiteSpace(c))));

        // §127: 맑은 고딕에서 tofu로 깨지는 것으로 이미 확인된 글리프(✓, U+2713)가 남아있지 않아야 한다.
        Assert.DoesNotContain('✓', fullText);

        // 24개 장 제목 + 마스터 §69가 요구한 핵심 섹션들이 실제 렌더링된 텍스트에 전부 나타나야 한다.
        string[] expectedHeadings =
        {
            "1.CADWorkAssistant소개", "3.화면구성", "4.프로젝트관리", "6.길이측정", "7.면적측정",
            "8.수직면적계산", "9.파라펫계산", "15.수량이력및검산", "16.Excel수량산출서",
            "17.PDF산출근거서", "19.프로젝트파일관리", "22.문제해결", "24.용어정리",
        };
        foreach (var heading in expectedHeadings)
        {
            Assert.Contains(heading, fullText);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CADWorkAssistant.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate repo root (CADWorkAssistant.sln) from " + AppContext.BaseDirectory);
    }
}
