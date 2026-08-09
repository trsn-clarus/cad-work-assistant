namespace CADWorkAssistant.Core.Plot;

/// <summary>`PlotDrawingPdf` IPC 응답 (Milestone 11 §50). Resolved* 필드는 §39 "Preset Resolution
/// 결과"를 그대로 담는다 - 사용자가 요청한 의도(A3·흑백)가 실제로 어떤 장치/용지/스타일로
/// 실행됐는지 Desktop이 그대로 보여줄 수 있게 한다.</summary>
public sealed class PlotDrawingPdfResponse
{
    public PlotDrawingPdfResponse(
        string outputFile,
        string resolvedDevice,
        string resolvedMediaCanonical,
        string resolvedMediaDisplay,
        string resolvedStyleSheet,
        double paperWidthMm,
        double paperHeightMm,
        CadPlotOrientation resolvedOrientation,
        long elapsedMs,
        string? warning)
    {
        OutputFile = outputFile;
        ResolvedDevice = resolvedDevice;
        ResolvedMediaCanonical = resolvedMediaCanonical;
        ResolvedMediaDisplay = resolvedMediaDisplay;
        ResolvedStyleSheet = resolvedStyleSheet;
        PaperWidthMm = paperWidthMm;
        PaperHeightMm = paperHeightMm;
        ResolvedOrientation = resolvedOrientation;
        ElapsedMs = elapsedMs;
        Warning = warning;
    }

    public string OutputFile { get; }

    public string ResolvedDevice { get; }

    public string ResolvedMediaCanonical { get; }

    public string ResolvedMediaDisplay { get; }

    public string ResolvedStyleSheet { get; }

    public double PaperWidthMm { get; }

    public double PaperHeightMm { get; }

    /// <summary>Auto가 아니라 실제로 적용된 방향(§25).</summary>
    public CadPlotOrientation ResolvedOrientation { get; }

    public long ElapsedMs { get; }

    /// <summary>§41 - "색상 변환에 따라 일부 객체가 예상과 다르게 출력될 수 있습니다" 같은 짧은 안내.
    /// 모든 monochrome 출력에 경고하지 않는다 - null이 정상이다.</summary>
    public string? Warning { get; }
}
