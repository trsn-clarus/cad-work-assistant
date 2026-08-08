namespace CADWorkAssistant.Core.Cad;

/// <summary>
/// 현재 AutoCAD 활성 문서의 스냅샷. GetDrawingContext IPC 응답 payload (§18).
/// </summary>
public sealed class DrawingContext
{
    public DrawingContext(
        string documentDisplayName,
        string? fullPath,
        bool isSaved,
        bool isReadOnly,
        string layout,
        DrawingUnit units,
        int documentCount)
    {
        DocumentDisplayName = documentDisplayName;
        FullPath = fullPath;
        IsSaved = isSaved;
        IsReadOnly = isReadOnly;
        Layout = layout;
        Units = units;
        DocumentCount = documentCount;
    }

    /// <summary>화면에 보여줄 파일명 (예: "OO학교_건축.dwg" 또는 저장 전이면 "Drawing1.dwg").</summary>
    public string DocumentDisplayName { get; }

    /// <summary>저장된 적 없는 도면(Drawing1.dwg 등)이면 null (§20).</summary>
    public string? FullPath { get; }

    public bool IsSaved { get; }

    public bool IsReadOnly { get; }

    public string Layout { get; }

    public DrawingUnit Units { get; }

    public int DocumentCount { get; }
}
