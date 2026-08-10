using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Pdf.IO;

namespace CADWorkAssistant.Documents.Pdf.UserManual;

/// <summary>
/// Milestone 13 Part B - 파싱된 <see cref="UserManualDocument"/> -> 실제 .pdf. QuantityPdfBuilder와
/// 완전히 같은 원칙을 따른다(§109-113): PDFsharp/MigraDoc을 직접 다루는 것은 Documents뿐이고,
/// WindowsKoreanFontResolver를 그대로 재사용하며, 저장은 원자적이다. 목차는 각 장 제목 Paragraph에
/// Bookmark를 심고 목차 페이지에서 PageRefField로 실제 페이지 번호를 참조한다(§120) - 고정된 페이지
/// 번호를 하드코딩하지 않는다.
/// </summary>
public sealed class UserManualPdfBuilder
{
    private static readonly Color AccentColor = new(0x1D, 0x6F, 0x8F);
    private static readonly Color MutedTextColor = new(0x52, 0x61, 0x70);
    private static readonly Color HeaderFill = new(0xEE, 0xF2, 0xF5);
    private static readonly Color BorderColor = new(0xCA, 0xD3, 0xDC);
    private static readonly Color CodeBoxFill = new(0xF7, 0xF8, 0xFA);

    private static readonly Regex ChapterHeadingPattern = new(@"^(?<num>\d+)\.\s*(?<title>.+)$", RegexOptions.Compiled);

    public UserManualPdfResult BuildAndSave(UserManualDocument document, string version, DateTimeOffset generatedAt, string targetPath)
    {
        WindowsKoreanFontResolver.EnsureRegistered();

        var chapters = CollectChapters(document);

        var pdfDocument = new Document();
        pdfDocument.Info.Title = "CAD Work Assistant 사용설명서";
        pdfDocument.Info.Subject = $"CAD Work Assistant {version} 사용설명서";

        DefineStyles(pdfDocument);

        var section = pdfDocument.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.TopMargin = "2cm";
        section.PageSetup.BottomMargin = "1.6cm";
        section.PageSetup.LeftMargin = "2cm";
        section.PageSetup.RightMargin = "2cm";

        BuildFooter(section);

        RenderBlocks(section, document, chapters, version, generatedAt);

        var renderer = new PdfDocumentRenderer { Document = pdfDocument };
        renderer.RenderDocument();
        var pageCount = renderer.PdfDocument.PageCount;

        SaveAtomically(renderer, targetPath);

        return new UserManualPdfResult { FilePath = targetPath, PageCount = pageCount, ChapterCount = chapters.Count };
    }

    private sealed record ChapterEntry(int Number, string Title, string BookmarkName);

    private static List<ChapterEntry> CollectChapters(UserManualDocument document)
    {
        var chapters = new List<ChapterEntry>();
        foreach (var block in document.Blocks)
        {
            if (block is not Heading1Block heading)
            {
                continue;
            }

            var match = ChapterHeadingPattern.Match(heading.Text);
            if (!match.Success)
            {
                continue;
            }

            var number = int.Parse(match.Groups["num"].Value, CultureInfo.InvariantCulture);
            chapters.Add(new ChapterEntry(number, match.Groups["title"].Value, $"chapter-{number}"));
        }

        return chapters;
    }

    private static void DefineStyles(Document document)
    {
        var normal = document.Styles["Normal"]!;
        normal.Font.Name = "Malgun Gothic";
        normal.Font.Size = 10;
        normal.Font.Color = Colors.Black;
        normal.ParagraphFormat.SpaceAfter = "0.25cm";
        normal.ParagraphFormat.LineSpacing = 1.15;
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;

        var heading1 = document.Styles.AddStyle("Heading1", "Normal");
        heading1.Font.Size = 18;
        heading1.Font.Bold = true;
        heading1.Font.Color = AccentColor;
        heading1.ParagraphFormat.SpaceAfter = "0.5cm";
        heading1.ParagraphFormat.KeepWithNext = true;

        var heading2 = document.Styles.AddStyle("Heading2", "Normal");
        heading2.Font.Size = 13;
        heading2.Font.Bold = true;
        heading2.ParagraphFormat.SpaceBefore = "0.4cm";
        heading2.ParagraphFormat.SpaceAfter = "0.2cm";
        heading2.ParagraphFormat.KeepWithNext = true;

        var muted = document.Styles.AddStyle("ManualMuted", "Normal");
        muted.Font.Color = MutedTextColor;
        muted.Font.Size = 8.5;
    }

    private static void BuildFooter(Section section)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = MutedTextColor;
        footer.AddText("CAD Work Assistant 사용설명서");
        footer.AddTab();
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        section.PageSetup.FooterDistance = "0.8cm";
        var tabStops = footer.Format.TabStops;
        tabStops.ClearAll();
        tabStops.AddTabStop(section.PageSetup.PageWidth - section.PageSetup.LeftMargin - section.PageSetup.RightMargin,
            TabAlignment.Right);
    }

    private static void RenderBlocks(Section section, UserManualDocument document, List<ChapterEntry> chapters,
        string version, DateTimeOffset generatedAt)
    {
        var insideToc = false;
        var coverTaglineRendered = false;

        for (var i = 0; i < document.Blocks.Count; i++)
        {
            var block = document.Blocks[i];

            switch (block)
            {
                case Heading1Block heading:
                    insideToc = false;
                    var isCover = i == 0;
                    var match = ChapterHeadingPattern.Match(heading.Text);

                    var headingParagraph = section.AddParagraph(heading.Text);
                    headingParagraph.Style = "Heading1";
                    if (isCover)
                    {
                        headingParagraph.Format.Font.Size = 26;
                    }
                    else
                    {
                        headingParagraph.Format.PageBreakBefore = true;
                    }

                    if (match.Success)
                    {
                        var number = int.Parse(match.Groups["num"].Value, CultureInfo.InvariantCulture);
                        headingParagraph.AddBookmark($"chapter-{number}");
                    }
                    else if (heading.Text == "목차")
                    {
                        insideToc = true;
                        RenderTocEntries(section, chapters);
                    }

                    break;

                case Heading2Block heading2:
                    if (insideToc)
                    {
                        break;
                    }

                    var h2 = section.AddParagraph(heading2.Text);
                    h2.Style = "Heading2";
                    break;

                case ParagraphBlock paragraph:
                    if (insideToc)
                    {
                        break;
                    }

                    if (!coverTaglineRendered)
                    {
                        // 문서의 첫 문단은 항상 표지 태그라인이다(제목 H1 -> 부제 H2 -> 태그라인
                        // 순서로 파싱됨) - 여기 바로 뒤에 버전/생성일을 이어 붙인다.
                        coverTaglineRendered = true;

                        var coverTagline = section.AddParagraph();
                        AddInlineRuns(coverTagline, paragraph.Runs);
                        coverTagline.Format.Font.Size = 12;
                        coverTagline.Format.Font.Color = MutedTextColor;
                        coverTagline.Format.SpaceAfter = "0.6cm";

                        var coverMeta = section.AddParagraph();
                        coverMeta.AddText($"대상 버전: {version}    생성일: {generatedAt:yyyy-MM-dd}");
                        coverMeta.Format.Font.Size = 9.5;
                        coverMeta.Format.Font.Color = MutedTextColor;
                        coverMeta.Format.SpaceAfter = "1cm";
                        break;
                    }

                    var p = section.AddParagraph();
                    AddInlineRuns(p, paragraph.Runs);
                    break;

                case BulletItemBlock bullet:
                    if (insideToc)
                    {
                        break;
                    }

                    var bp = section.AddParagraph();
                    bp.Format.LeftIndent = "0.6cm";
                    bp.Format.FirstLineIndent = "-0.6cm";
                    bp.AddText("• ");
                    AddInlineRuns(bp, bullet.Runs);
                    break;

                case NumberedItemBlock numbered:
                    if (insideToc)
                    {
                        break;
                    }

                    var np = section.AddParagraph();
                    np.Format.LeftIndent = "0.9cm";
                    np.Format.FirstLineIndent = "-0.9cm";
                    np.AddText($"{numbered.Marker} ");
                    AddInlineRuns(np, numbered.Runs);
                    break;

                case ManualImageBlock image:
                    RenderImage(section, image);
                    break;

                case ManualCodeBlock code:
                    RenderCodeBlock(section, code);
                    break;

                case ManualTableBlock table:
                    RenderTable(section, table);
                    break;

                case RuleBlock:
                    // 문서 안의 "---"는 항상 다음 Heading1 바로 앞에만 나온다 - Heading1의
                    // PageBreakBefore가 이미 그 역할을 하므로 여기서는 아무것도 하지 않는다.
                    break;
            }
        }
    }

    private static void RenderTocEntries(Section section, List<ChapterEntry> chapters)
    {
        var tabWidth = section.PageSetup.PageWidth - section.PageSetup.LeftMargin - section.PageSetup.RightMargin;

        foreach (var chapter in chapters)
        {
            var line = section.AddParagraph();
            line.Format.TabStops.AddTabStop(tabWidth, TabAlignment.Right, TabLeader.Dots);
            line.AddText($"{chapter.Number}. {chapter.Title}");
            line.AddTab();
            line.AddPageRefField(chapter.BookmarkName);
            line.Format.SpaceAfter = "0.15cm";
        }
    }

    private static void AddInlineRuns(Paragraph paragraph, IReadOnlyList<InlineRun> runs)
    {
        foreach (var run in runs)
        {
            switch (run.Style)
            {
                case InlineStyle.Bold:
                    paragraph.AddFormattedText(run.Text, TextFormat.Bold);
                    break;
                case InlineStyle.Code:
                    var codeFormat = paragraph.AddFormattedText(run.Text);
                    codeFormat.Font.Name = "Consolas";
                    codeFormat.Font.Size = 9.5;
                    codeFormat.Font.Color = AccentColor;
                    break;
                default:
                    paragraph.AddText(run.Text);
                    break;
            }
        }
    }

    private static void RenderImage(Section section, ManualImageBlock image)
    {
        if (!File.Exists(image.AbsolutePath))
        {
            // §116: Release 빌드 전체를 실패시켜야 한다 - 스크린샷 누락을 조용히 건너뛰지 않는다.
            throw new FileNotFoundException(
                $"User manual references a screenshot that does not exist: '{image.AbsolutePath}' (alt: \"{image.AltText}\").",
                image.AbsolutePath);
        }

        var contentWidth = section.PageSetup.PageWidth - section.PageSetup.LeftMargin - section.PageSetup.RightMargin;

        var imageParagraph = section.AddParagraph();
        imageParagraph.Format.SpaceBefore = "0.2cm";
        var picture = imageParagraph.AddImage(image.AbsolutePath);
        picture.LockAspectRatio = true;
        picture.Width = contentWidth;

        var border = new Color(0xD8, 0xDD, 0xE3);
        picture.LineFormat.Width = 0.5;
        picture.LineFormat.Color = border;

        if (!string.IsNullOrWhiteSpace(image.AltText))
        {
            var caption = section.AddParagraph(image.AltText);
            caption.Style = "ManualMuted";
            caption.Format.Alignment = ParagraphAlignment.Center;
            caption.Format.SpaceAfter = "0.4cm";
        }
    }

    private static void RenderCodeBlock(Section section, ManualCodeBlock code)
    {
        var p = section.AddParagraph();
        p.Format.Shading.Color = CodeBoxFill;
        p.Format.Borders.Width = 0.5;
        p.Format.Borders.Color = BorderColor;
        p.Format.LeftIndent = "0.3cm";
        p.Format.RightIndent = "0.3cm";
        p.Format.SpaceBefore = "0.15cm";
        p.Format.SpaceAfter = "0.35cm";
        p.Format.Font.Name = "Consolas";
        p.Format.Font.Size = 8.5;

        var lines = code.Text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                p.AddLineBreak();
            }

            p.AddText(lines[i].Length == 0 ? " " : lines[i]);
        }
    }

    private static void RenderTable(Section section, ManualTableBlock model)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = BorderColor;
        table.Format.Font.Size = 9.5;
        table.Format.SpaceAfter = "0.4cm";

        var contentWidth = section.PageSetup.PageWidth - section.PageSetup.LeftMargin - section.PageSetup.RightMargin;
        var columnWidth = contentWidth / model.Headers.Count;
        for (var i = 0; i < model.Headers.Count; i++)
        {
            table.AddColumn(columnWidth);
        }

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = HeaderFill;
        header.Format.Font.Bold = true;
        for (var i = 0; i < model.Headers.Count; i++)
        {
            header.Cells[i].AddParagraph(model.Headers[i]);
        }

        foreach (var row in model.Rows)
        {
            var r = table.AddRow();
            for (var i = 0; i < row.Count; i++)
            {
                r.Cells[i].AddParagraph(row[i]);
            }
        }
    }

    /// <summary>QuantityPdfBuilder.SaveAtomically와 완전히 같은 절차(§81-85).</summary>
    private static void SaveAtomically(PdfDocumentRenderer renderer, string targetPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? ".";
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"~cwa_{Guid.NewGuid():N}.pdf");

        try
        {
            renderer.PdfDocument.Save(tempPath);

            using (var verify = PdfReader.Open(tempPath, PdfDocumentOpenMode.Import))
            {
                if (verify.PageCount == 0)
                {
                    throw new InvalidOperationException("Generated user manual PDF has no pages.");
                }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup - 원래 오류를 가리지 않는다.
        }
    }
}
