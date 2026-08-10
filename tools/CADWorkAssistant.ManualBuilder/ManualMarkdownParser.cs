using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CADWorkAssistant.Documents.Pdf.UserManual;

namespace CADWorkAssistant.ManualBuilder;

/// <summary>
/// Milestone 13 Part B §110-112 - 사용설명서가 실제로 쓰는 가벼운 Markdown 하위집합만 다루는
/// 전용 파서다(범용 CommonMark 구현이 아니다 - CLAUDE.md 절대 원칙 6, 필요한 만큼만 구현한다).
/// 지원: '# '/'## ' 제목, '---' 구분선, ``` 코드 펜스, '![alt](path)' 이미지, '- ' 글머리 목록,
/// '1. ' 번호 목록(각각 다음 블록이 시작되기 전까지 후속 줄을 lazy continuation으로 이어붙임),
/// '\|...\|' 파이프 표, 그리고 나머지는 일반 문단(**bold**/`code` 인라인 서식 지원).
/// </summary>
public static class ManualMarkdownParser
{
    private static readonly Regex ImagePattern = new(@"^!\[(?<alt>[^\]]*)\]\((?<path>[^)]+)\)$", RegexOptions.Compiled);
    private static readonly Regex BulletPattern = new(@"^-\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex NumberedPattern = new(@"^(?<marker>\d+\.)\s+(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex InlinePattern = new(@"\*\*(?<bold>[^*]+)\*\*|`(?<code>[^`]+)`", RegexOptions.Compiled);

    public static UserManualDocument Parse(string markdownText, string baseDirectory)
    {
        var lines = markdownText.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<UserManualBlock>();
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }

            if (line == "---")
            {
                blocks.Add(new RuleBlock());
                i++;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                blocks.Add(new Heading2Block { Text = line[3..].Trim() });
                i++;
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                blocks.Add(new Heading1Block { Text = line[2..].Trim() });
                i++;
                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                i++;
                var codeLines = new List<string>();
                while (i < lines.Length && lines[i].TrimEnd() != "```")
                {
                    codeLines.Add(lines[i]);
                    i++;
                }

                i++; // closing fence
                blocks.Add(new ManualCodeBlock { Text = string.Join("\n", codeLines) });
                continue;
            }

            var imageMatch = ImagePattern.Match(line);
            if (imageMatch.Success)
            {
                var relativePath = imageMatch.Groups["path"].Value;
                var absolutePath = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
                blocks.Add(new ManualImageBlock { AltText = imageMatch.Groups["alt"].Value, AbsolutePath = absolutePath });
                i++;
                continue;
            }

            if (line.TrimStart().StartsWith("|", StringComparison.Ordinal))
            {
                var tableLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith("|", StringComparison.Ordinal))
                {
                    tableLines.Add(lines[i]);
                    i++;
                }

                blocks.Add(ParseTable(tableLines));
                continue;
            }

            var bulletMatch = BulletPattern.Match(line);
            if (bulletMatch.Success)
            {
                var text = bulletMatch.Groups[1].Value;
                i++;
                while (i < lines.Length && IsContinuationLine(lines[i]))
                {
                    text += " " + lines[i].Trim();
                    i++;
                }

                blocks.Add(new BulletItemBlock { Runs = ParseInline(text) });
                continue;
            }

            var numberedMatch = NumberedPattern.Match(line);
            if (numberedMatch.Success)
            {
                var marker = numberedMatch.Groups["marker"].Value;
                var text = numberedMatch.Groups["text"].Value;
                i++;
                while (i < lines.Length && IsContinuationLine(lines[i]))
                {
                    text += " " + lines[i].Trim();
                    i++;
                }

                blocks.Add(new NumberedItemBlock { Marker = marker, Runs = ParseInline(text) });
                continue;
            }

            // 일반 문단 - 다음 블록이 시작되기 전까지 이어지는 줄을 한 문단으로 합친다.
            var paragraphLines = new List<string> { line.Trim() };
            i++;
            while (i < lines.Length && IsContinuationLine(lines[i]))
            {
                paragraphLines.Add(lines[i].Trim());
                i++;
            }

            blocks.Add(new ParagraphBlock { Runs = ParseInline(string.Join(" ", paragraphLines)) });
        }

        return new UserManualDocument { Blocks = blocks };
    }

    private static bool IsContinuationLine(string line)
    {
        if (line.Trim().Length == 0)
        {
            return false;
        }

        if (line == "---")
        {
            return false;
        }

        if (line.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.StartsWith("```", StringComparison.Ordinal))
        {
            return false;
        }

        if (ImagePattern.IsMatch(line))
        {
            return false;
        }

        if (line.TrimStart().StartsWith("|", StringComparison.Ordinal))
        {
            return false;
        }

        if (BulletPattern.IsMatch(line) || NumberedPattern.IsMatch(line))
        {
            return false;
        }

        return true;
    }

    private static ManualTableBlock ParseTable(List<string> tableLines)
    {
        if (tableLines.Count < 2)
        {
            throw new FormatException($"Malformed pipe table (need header + separator row, got {tableLines.Count} lines): {string.Join(" / ", tableLines)}");
        }

        var rows = tableLines.Select(SplitRow).ToList();
        var separator = rows[1];
        if (separator.Any(cell => cell.Trim('-', ':', ' ').Length != 0))
        {
            throw new FormatException($"Expected a '---' separator row as the second table row, got: {tableLines[1]}");
        }

        var headers = rows[0];
        var dataRows = rows.Skip(2).Select(r => (IReadOnlyList<string>)r).ToList();
        return new ManualTableBlock { Headers = headers, Rows = dataRows };
    }

    private static List<string> SplitRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }

    private static List<InlineRun> ParseInline(string text)
    {
        var runs = new List<InlineRun>();
        var pos = 0;

        foreach (Match match in InlinePattern.Matches(text))
        {
            if (match.Index > pos)
            {
                runs.Add(new InlineRun(text[pos..match.Index], InlineStyle.Plain));
            }

            runs.Add(match.Groups["bold"].Success
                ? new InlineRun(match.Groups["bold"].Value, InlineStyle.Bold)
                : new InlineRun(match.Groups["code"].Value, InlineStyle.Code));

            pos = match.Index + match.Length;
        }

        if (pos < text.Length)
        {
            runs.Add(new InlineRun(text[pos..], InlineStyle.Plain));
        }

        return runs;
    }
}
