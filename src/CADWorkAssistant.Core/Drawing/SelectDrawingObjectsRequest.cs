namespace CADWorkAssistant.Core.Drawing;

/// <summary>`SelectDrawingObjects` IPC 요청 payload.</summary>
public sealed class SelectDrawingObjectsRequest
{
    public SelectDrawingObjectsRequest(SelectionMode mode)
    {
        Mode = mode;
    }

    public SelectionMode Mode { get; }
}
