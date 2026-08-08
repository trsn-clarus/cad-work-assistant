namespace CADWorkAssistant.Core.Drawing;

/// <summary>`ZoomToBounds` IPC 요청 payload - SelectionSession.Bounds 등 Desktop이 이미 들고 있는
/// Bounds로 화면을 맞춘다 (§23, "선택 영역 보기"). Bounds 출처를 AutoCAD Selection으로 한정하지 않기
/// 위해 별도 "ZoomSelection" 명령 대신 범용 Bounds를 받는다.</summary>
public sealed class ZoomToBoundsRequest
{
    public ZoomToBoundsRequest(CadBoundsDto bounds)
    {
        Bounds = bounds;
    }

    public CadBoundsDto Bounds { get; }
}
