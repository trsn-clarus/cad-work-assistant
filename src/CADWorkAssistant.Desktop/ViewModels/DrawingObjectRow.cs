namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>Selection 결과 테이블의 한 행. 표시 전용 - Length/Area의 Row 타입들과 같은 패턴.</summary>
public sealed class DrawingObjectRow
{
    public DrawingObjectRow(string handle, string objectType, string layer)
    {
        Handle = handle;
        ObjectType = objectType;
        Layer = layer;
    }

    public string Handle { get; }
    public string ObjectType { get; }
    public string Layer { get; }
}
