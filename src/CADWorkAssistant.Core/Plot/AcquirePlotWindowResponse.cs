namespace CADWorkAssistant.Core.Plot;

/// <summary>`AcquirePlotWindow` IPC 응답 (Milestone 11 §13, §54). 요청 payload는 없다 - "지금
/// Model Space에서 사용자가 두 모서리를 지정하게 해달라"는 요청 자체에 파라미터가 필요 없다
/// (ZoomExtents와 같은 관례).</summary>
public sealed class AcquirePlotWindowResponse
{
    public AcquirePlotWindowResponse(CadPlotWindowDto window)
    {
        Window = window;
    }

    public CadPlotWindowDto Window { get; }
}
