using System.Collections.Generic;

namespace CADWorkAssistant.Core.Plot;

/// <summary>
/// `GetPlotCapabilities` IPC 응답 (Milestone 11 §15, §51). Desktop이 Plot을 시작하기 전에 현재
/// AutoCAD에서 실제로 가능한 옵션을 조회한다 - 장치/용지/스타일을 하드코딩하지 않는다(§16).
/// </summary>
public sealed class PlotCapabilitiesResponse
{
    public PlotCapabilitiesResponse(
        IReadOnlyList<CadPlotDeviceDto> devices,
        IReadOnlyList<CadPlotMediaDto> media,
        IReadOnlyList<string> colorDependentStyleSheets,
        IReadOnlyList<string> namedStyleSheets,
        CadPlotStyleMode currentDrawingStyleMode,
        IReadOnlyList<CadPlotLayoutDto> layouts)
    {
        Devices = devices;
        Media = media;
        ColorDependentStyleSheets = colorDependentStyleSheets;
        NamedStyleSheets = namedStyleSheets;
        CurrentDrawingStyleMode = currentDrawingStyleMode;
        Layouts = layouts;
    }

    public IReadOnlyList<CadPlotDeviceDto> Devices { get; }

    /// <summary>§17에서 자동 선택된(또는 향후 사용자가 고를) PDF 장치가 실제 지원하는 용지 목록 -
    /// 장치를 정하지 않고는 용지 목록을 물을 수 없다(§19, PlotConfig.GetMediaBounds는 장치별로
    /// 다르다).</summary>
    public IReadOnlyList<CadPlotMediaDto> Media { get; }

    /// <summary>§30, §33 - Color-Dependent(CTB) 목록.</summary>
    public IReadOnlyList<string> ColorDependentStyleSheets { get; }

    /// <summary>§30, §33 - Named(STB) 목록.</summary>
    public IReadOnlyList<string> NamedStyleSheets { get; }

    /// <summary>§31 - 현재 도면이 CTB/STB 중 어느 모드인지.</summary>
    public CadPlotStyleMode CurrentDrawingStyleMode { get; }

    public IReadOnlyList<CadPlotLayoutDto> Layouts { get; }
}
