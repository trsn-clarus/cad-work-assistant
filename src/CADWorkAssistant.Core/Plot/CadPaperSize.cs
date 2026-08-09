using System.Collections.Generic;

namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §20-23 - 사람이 이해하는 용지 크기. Portrait 기준(Width &lt; Height)으로
/// mm 단위 물리 치수를 갖는다 - 실제 매칭은 <see cref="PlotPaperMatcher"/>가 AutoCAD 장치의 실제
/// Canonical Media 치수와 이 값을 비교해서 한다(문자열에 "ISO_A3"가 들어있는지로 판단하지
/// 않는다, §21).</summary>
public sealed class CadPaperSize
{
    public CadPaperSize(string name, double portraitWidthMm, double portraitHeightMm)
    {
        Name = name;
        PortraitWidthMm = portraitWidthMm;
        PortraitHeightMm = portraitHeightMm;
    }

    public string Name { get; }

    public double PortraitWidthMm { get; }

    public double PortraitHeightMm { get; }

    public string DisplayText => $"{Name} {PortraitWidthMm:0} × {PortraitHeightMm:0} mm";
}

/// <summary>Milestone 11 §23 - 첫 버전은 A4/A3만 지원한다. A2/A1/A0는 Future Roadmap(§23).</summary>
public static class CadPaperSizeCatalog
{
    public static readonly CadPaperSize A4 = new("A4", 210, 297);

    public static readonly CadPaperSize A3 = new("A3", 297, 420);

    public static IReadOnlyList<CadPaperSize> BuiltIn { get; } = new[] { A4, A3 };
}
