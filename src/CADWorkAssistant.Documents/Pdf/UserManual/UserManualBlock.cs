using System.Collections.Generic;

namespace CADWorkAssistant.Documents.Pdf.UserManual;

public enum InlineStyle
{
    Plain,
    Bold,
    Code,
}

public readonly record struct InlineRun(string Text, InlineStyle Style);

/// <summary>사용설명서 Markdown을 파싱한 결과의 최소 단위. `tools/CADWorkAssistant.ManualBuilder`의
/// 파서가 만들고, <see cref="UserManualPdfBuilder"/>가 소비한다 - Documents는 렌더링만 담당한다
/// (§109-113, QuantityPdfBuilder와 같은 원칙).</summary>
public abstract class UserManualBlock
{
}

public sealed class Heading1Block : UserManualBlock
{
    public required string Text { get; init; }
}

public sealed class Heading2Block : UserManualBlock
{
    public required string Text { get; init; }
}

public sealed class ParagraphBlock : UserManualBlock
{
    public required IReadOnlyList<InlineRun> Runs { get; init; }
}

public sealed class BulletItemBlock : UserManualBlock
{
    public required IReadOnlyList<InlineRun> Runs { get; init; }
}

public sealed class NumberedItemBlock : UserManualBlock
{
    public required string Marker { get; init; }

    public required IReadOnlyList<InlineRun> Runs { get; init; }
}

public sealed class ManualImageBlock : UserManualBlock
{
    public required string AltText { get; init; }

    public required string AbsolutePath { get; init; }
}

public sealed class ManualCodeBlock : UserManualBlock
{
    public required string Text { get; init; }
}

public sealed class ManualTableBlock : UserManualBlock
{
    public required IReadOnlyList<string> Headers { get; init; }

    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
}

public sealed class RuleBlock : UserManualBlock
{
}
