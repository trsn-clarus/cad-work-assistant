namespace CADWorkAssistant.Core.Plot;

/// <summary>
/// Milestone 11 §12 - Milestone 5의 <see cref="CADWorkAssistant.Core.Drawing.CadBoundsDto"/>(선택된
/// 객체들의 bounding box, 3D)와 의도적으로 분리한 타입이다. Plot Window는 사용자가 직접 두 모서리를
/// 지정한 2D 평면 영역이지 "선택된 객체의 경계"가 아니다 - 개념이 다르면 같은 DTO를 억지로 재사용하지
/// 않는다(§12, Milestone 3의 SelectionOutcome&lt;T&gt; 제네릭화와 반대되는 판단 - 그때는 진짜 중복이었고
/// 이번은 우연히 모양이 비슷할 뿐인 다른 개념이다). WCS 좌표를 그대로 담는다 - 실제 UCS/View 회전 상태에서의
/// 정확성은 Real AutoCAD 검증 대상이다(§14, §127-128).
/// </summary>
public sealed class CadPlotWindowDto
{
    public CadPlotWindowDto(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;
}
