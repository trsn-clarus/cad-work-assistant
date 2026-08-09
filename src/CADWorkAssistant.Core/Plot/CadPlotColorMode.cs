namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §35-36 - 사용자의 "의도"다. 실제로 어떤 Plot Style Sheet 파일이 적용될지는
/// <see cref="PlotStyleResolver"/>가 현재 도면의 CTB/STB 모드와 실제 사용 가능한 목록을 보고 따로
/// 결정한다 - "컬러"를 골랐다고 무조건 acad.ctb를 강제하지 않는다(§35).</summary>
public enum CadPlotColorMode
{
    /// <summary>도면에 이미 지정된 색상/Plot Style을 그대로 쓴다 - 가장 안전한 기본값(§35).</summary>
    KeepExisting,

    /// <summary>흑백 출력을 시도한다. CTB 도면에서 monochrome.ctb가 있을 때만 적용되고, 없으면
    /// Unavailable로 보고한다(§36).</summary>
    Monochrome
}
