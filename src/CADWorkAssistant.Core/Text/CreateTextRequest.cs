namespace CADWorkAssistant.Core.Text;

/// <summary>`CreateText` IPC 요청 (Milestone 12 §35, §37). LayerName/Color가 null이면 AutoCAD
/// Plugin이 현재 Layer(§34)/ByLayer(§26)를 기본값으로 쓴다 - Desktop이 매번 현재 값을 미리 조회해서
/// 채워 보내지 않아도 된다.</summary>
public sealed class CreateTextRequest
{
    public CreateTextRequest(
        CadTextEntityType entityType,
        string content,
        double height,
        string? layerName,
        CadColorDto? color,
        CadPointDto insertionPoint)
    {
        EntityType = entityType;
        Content = content;
        Height = height;
        LayerName = layerName;
        Color = color;
        InsertionPoint = insertionPoint;
    }

    public CadTextEntityType EntityType { get; }

    public string Content { get; }

    public double Height { get; }

    /// <summary>null이면 현재 Layer를 쓴다(§34).</summary>
    public string? LayerName { get; }

    /// <summary>null이면 ByLayer를 쓴다(§26, 가장 안전한 기본값).</summary>
    public CadColorDto? Color { get; }

    public CadPointDto InsertionPoint { get; }
}
