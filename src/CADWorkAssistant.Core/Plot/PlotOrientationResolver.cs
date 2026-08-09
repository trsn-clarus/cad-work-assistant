namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §24-26 - Window bounds의 종횡비로 Auto 방향을 결정하는 순수 로직. AutoCAD
/// API를 전혀 모른다.</summary>
public static class PlotOrientationResolver
{
    /// <summary>
    /// requested가 Auto가 아니면 그대로 돌려준다. Auto인데 참고할 content가 없으면(예: Current
    /// Layout에서 아직 아무 창도 지정하지 않음) Portrait를 안전한 기본값으로 쓴다 - CadPaperSize
    /// 자체가 Portrait 기준(Width &lt; Height)으로 정의돼 있다.
    /// </summary>
    public static CadPlotOrientation Resolve(CadPlotOrientation requested, CadPlotWindowDto? window)
    {
        if (requested != CadPlotOrientation.Auto)
        {
            return requested;
        }

        if (window is null || window.Width <= 0 || window.Height <= 0)
        {
            return CadPlotOrientation.Portrait;
        }

        return window.Width > window.Height ? CadPlotOrientation.Landscape : CadPlotOrientation.Portrait;
    }
}
