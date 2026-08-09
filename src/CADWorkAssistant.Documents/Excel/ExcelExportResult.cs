namespace CADWorkAssistant.Documents.Excel;

/// <summary>Milestone 9 §42 - 성공 UX("Excel 저장 완료" + 파일명)에 필요한 최소 정보.</summary>
public sealed class ExcelExportResult
{
    public required string FilePath { get; init; }

    public required int RecordCount { get; init; }
}
