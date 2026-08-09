namespace CADWorkAssistant.Core.Text;

/// <summary>`AcquireTextInsertionPoint` IPC 응답 - AcquirePlotWindow(Milestone 11)와 같은 이유로
/// 별도 명령을 둔다: "지금 AutoCAD에서 사용자가 점 하나를 찍게 해달라"는 요청 자체에 payload가
/// 필요 없다.</summary>
public sealed class AcquireTextInsertionPointResponse
{
    public AcquireTextInsertionPointResponse(CadPointDto point)
    {
        Point = point;
    }

    public CadPointDto Point { get; }
}
