namespace CADWorkAssistant.Core.Drawing;

public sealed class ExportSelectionResponse
{
    public ExportSelectionResponse(int objectCount, string filePath)
    {
        ObjectCount = objectCount;
        FilePath = filePath;
    }

    public int ObjectCount { get; }

    public string FilePath { get; }
}
