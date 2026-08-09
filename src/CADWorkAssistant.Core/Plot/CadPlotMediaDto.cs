namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §19-22 - AutoCAD 장치가 실제로 지원하는 용지 하나. WidthMm/HeightMm은
/// AutoCAD Plugin이 PlotConfig.GetMediaBounds(canonicalName).PageSize를 읽어 채운다(장치 단위와
/// 무관하게 항상 mm로 정규화, §20) - <see cref="PlotPaperMatcher"/>가 이 실측값으로 A4/A3를
/// 매칭한다. CanonicalName은 내부 식별자, LocaleName은 AutoCAD가 제공하는 사람이 읽는 이름
/// (참고용 - 메인 UI에는 CadPaperSize.DisplayText를 쓰고 이 값은 Technical Details에만 노출한다,
/// §73).</summary>
public sealed class CadPlotMediaDto
{
    public CadPlotMediaDto(string canonicalName, string localeName, double widthMm, double heightMm)
    {
        CanonicalName = canonicalName;
        LocaleName = localeName;
        WidthMm = widthMm;
        HeightMm = heightMm;
    }

    public string CanonicalName { get; }

    public string LocaleName { get; }

    public double WidthMm { get; }

    public double HeightMm { get; }
}
