namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §15-18 - AutoCAD Plot 장치 하나. Plotter 이름을 하드코딩하지 않고(§16)
/// 실제 PlotConfigManager.Devices를 순회해 만든다. <see cref="IsPdfCapable"/>는 PlotConfig의
/// IsPlotToFile+DefaultFileExtension=="pdf"로 판단한다 - 이름에 "PDF"가 들어있는지로 추측하지
/// 않는다.</summary>
public sealed class CadPlotDeviceDto
{
    public CadPlotDeviceDto(string name, bool isPdfCapable)
    {
        Name = name;
        IsPdfCapable = isPdfCapable;
    }

    public string Name { get; }

    public bool IsPdfCapable { get; }
}
