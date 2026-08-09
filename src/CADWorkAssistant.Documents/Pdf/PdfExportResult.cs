namespace CADWorkAssistant.Documents.Pdf;

/// <summary>Milestone 10 §78, §161 - 성공 UX + 로깅에 필요한 최소 정보. PageCount는 반드시
/// PdfDocument.Save() 이전에 읽어야 한다 - Save 이후에는 PdfSharp이 문서를 read-only로 잠그고
/// PageCount 접근 시 InvalidOperationException을 던진다(실제로 겪은 문제, §169 참고).</summary>
public sealed class PdfExportResult
{
    public required string FilePath { get; init; }

    public required int RecordCount { get; init; }

    public required int PageCount { get; init; }
}
