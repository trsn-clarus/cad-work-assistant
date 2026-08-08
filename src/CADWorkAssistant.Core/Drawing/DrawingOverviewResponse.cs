namespace CADWorkAssistant.Core.Drawing;

/// <summary>`GetDrawingOverview` IPC 응답 payload (§64) - 복잡한 Model Space 탐색의 최소 기반.
/// Cluster 자동 탐지 등은 이번 범위 밖이다 (§65) - Extents/개수만 제공한다.</summary>
public sealed class DrawingOverviewResponse
{
    public DrawingOverviewResponse(CadBoundsDto? extents, int objectCount, int layerCount)
    {
        Extents = extents;
        ObjectCount = objectCount;
        LayerCount = layerCount;
    }

    /// <summary>도면이 비어 있으면(ModelSpace에 객체가 하나도 없으면) null.</summary>
    public CadBoundsDto? Extents { get; }

    public int ObjectCount { get; }

    public int LayerCount { get; }
}
