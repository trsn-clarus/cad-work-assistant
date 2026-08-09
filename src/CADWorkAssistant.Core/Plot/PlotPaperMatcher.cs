using System;
using System.Collections.Generic;
using System.Linq;

namespace CADWorkAssistant.Core.Plot;

/// <summary>
/// Milestone 11 §21-23 - AutoCAD 장치가 실제로 보고하는 물리 치수(mm)를 기준으로 A4/A3를 찾는다.
/// Canonical Media 이름 문자열에 "ISO_A3"가 들어있는지로 판단하지 않는다(§21) - 장치/드라이버마다
/// 이름 규칙이 다를 수 있고, 실제로 신뢰할 수 있는 것은 PlotConfig.GetMediaBounds가 돌려주는 실측
/// PageSize뿐이다.
/// </summary>
public static class PlotPaperMatcher
{
    /// <summary>§22 - Plotter의 printable area/media 반올림 때문에 생기는 미세 오차 허용치.
    /// ViewModel에 매직 넘버를 두지 않는다(§22).</summary>
    public const double ToleranceMm = 2.0;

    /// <summary>
    /// media 목록에서 targetSize와 물리 치수가 일치하는 것을 찾는다. 용지가 Portrait로도 Landscape로도
    /// 보고될 수 있어(장치/드라이버 관례가 다르다) 두 방향 모두 비교한다 - 어느 쪽으로 매칭됐는지는
    /// 호출부가 Orientation으로 별도 결정한다(§24-26, 이 매처는 "이 용지가 맞는 크기인가"만 판단한다).
    /// </summary>
    public static CadPlotMediaDto? FindMatch(IReadOnlyList<CadPlotMediaDto> media, CadPaperSize targetSize)
    {
        foreach (var candidate in media)
        {
            if (IsMatch(candidate.WidthMm, candidate.HeightMm, targetSize) ||
                IsMatch(candidate.HeightMm, candidate.WidthMm, targetSize))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsMatch(double widthMm, double heightMm, CadPaperSize targetSize) =>
        Math.Abs(widthMm - targetSize.PortraitWidthMm) <= ToleranceMm &&
        Math.Abs(heightMm - targetSize.PortraitHeightMm) <= ToleranceMm;
}
