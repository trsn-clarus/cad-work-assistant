namespace CADWorkAssistant.Documents.Pdf.UserManual;

public sealed class UserManualPdfResult
{
    public required string FilePath { get; init; }

    public required int PageCount { get; init; }

    public required int ChapterCount { get; init; }
}
