namespace CADWorkAssistant.Core.Text;

/// <summary>`CreateText` IPC 응답 (Milestone 12 §74). 새로 만든 객체를 그대로 돌려줘 Desktop이
/// 별도 조회 없이 결과를 보여줄 수 있게 한다.</summary>
public sealed class CreateTextResponse
{
    public CreateTextResponse(CadTextObjectDto created)
    {
        Created = created;
    }

    public CadTextObjectDto Created { get; }
}
