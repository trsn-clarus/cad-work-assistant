using System.Collections.Generic;

namespace CADWorkAssistant.Documents.Pdf.UserManual;

/// <summary>파싱된 사용설명서 전체. 표지의 버전/생성일은 문서 자체가 아니라
/// <see cref="UserManualPdfBuilder.BuildAndSave"/>의 인자로 주입한다 - Markdown 소스에 버전을
/// 하드코딩하지 않는다(CLAUDE.md 절대 원칙 5).</summary>
public sealed class UserManualDocument
{
    public required IReadOnlyList<UserManualBlock> Blocks { get; init; }
}
