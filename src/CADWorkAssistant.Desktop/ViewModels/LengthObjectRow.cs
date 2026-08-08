namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>Length 결과 테이블의 한 행. 표시 전용 - 계산 로직을 갖지 않는다.</summary>
public sealed class LengthObjectRow
{
    public LengthObjectRow(string handle, string geometryType, string layer, string lengthDisplay)
    {
        Handle = handle;
        GeometryType = geometryType;
        Layer = layer;
        LengthDisplay = lengthDisplay;
    }

    public string Handle { get; }
    public string GeometryType { get; }
    public string Layer { get; }
    public string LengthDisplay { get; }
}
