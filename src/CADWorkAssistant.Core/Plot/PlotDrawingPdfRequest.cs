namespace CADWorkAssistant.Core.Plot;

/// <summary>`PlotDrawingPdf` IPC 요청 (Milestone 11 §49). Device/Media/Style을 직접 지정하지 않고
/// PaperSizeName+Orientation+ColorMode(사용자 의도)만 넘긴다 - 실제 장치/용지/스타일시트 이름으로
/// 바꾸는 일(Resolve)은 전부 AutoCAD Plugin 안에서 실제 Capability를 보고 일어난다(§38-39,
/// Desktop은 추측하지 않는다).</summary>
public sealed class PlotDrawingPdfRequest
{
    public PlotDrawingPdfRequest(
        CadPlotScope scope,
        string? layoutName,
        CadPlotWindowDto? window,
        string paperSizeName,
        CadPlotOrientation orientation,
        CadPlotColorMode colorMode,
        string targetFilePath)
    {
        Scope = scope;
        LayoutName = layoutName;
        Window = window;
        PaperSizeName = paperSizeName;
        Orientation = orientation;
        ColorMode = colorMode;
        TargetFilePath = targetFilePath;
    }

    public CadPlotScope Scope { get; }

    /// <summary>Scope == CurrentLayout일 때만 사용. null이면 AutoCAD의 현재 Layout을 쓴다.</summary>
    public string? LayoutName { get; }

    /// <summary>Scope == Window일 때만 사용 - AcquirePlotWindow 응답을 그대로 되돌려준다.</summary>
    public CadPlotWindowDto? Window { get; }

    /// <summary>"A4"/"A3" - CadPaperSizeCatalog.BuiltIn의 Name과 매칭한다.</summary>
    public string PaperSizeName { get; }

    public CadPlotOrientation Orientation { get; }

    public CadPlotColorMode ColorMode { get; }

    public string TargetFilePath { get; }
}
