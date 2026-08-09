using System;
using System.Collections.Generic;
using System.Linq;

namespace CADWorkAssistant.Core.Plot;

/// <summary>
/// Milestone 11 §29-36 - "컬러"/"흑백"이라는 사용자 의도를 실제 사용 가능한 Plot Style Sheet로
/// 바꾼다. 현재 도면의 CTB/STB 모드와 실제 조회된 목록만 근거로 삼는다 - monochrome.ctb가 항상
/// 있다고 가정하지 않고(§30), STB 도면에 CTB 파일을 강제하지 않는다(§33).
/// </summary>
public static class PlotStyleResolver
{
    public const string MonochromeStyleSheetName = "monochrome.ctb";

    public static PlotStyleResolution Resolve(
        CadPlotColorMode requested,
        CadPlotStyleMode currentDrawingStyleMode,
        IReadOnlyList<string> colorDependentStyleSheets,
        IReadOnlyList<string> namedStyleSheets)
    {
        if (requested == CadPlotColorMode.KeepExisting)
        {
            return PlotStyleResolution.KeepExisting();
        }

        // Monochrome 요청. §33: STB 도면에 monochrome.ctb를 억지로 적용하지 않는다 - STB에는
        // 동등한 표준 이름이 없고, 임의의 Named Style을 "흑백"이라고 추측하지 않는다.
        if (currentDrawingStyleMode == CadPlotStyleMode.Named)
        {
            _ = namedStyleSheets; // 현재 STB 목록은 참고용으로만 전달받는다 - Monochrome 추정에는 쓰지 않는다.
            return PlotStyleResolution.Unavailable(
                "이 도면은 명명된 플롯 스타일(STB)을 사용합니다 - 흑백 사전 설정은 색상 종속 플롯 스타일(CTB) 도면에서만 지원됩니다.");
        }

        var match = colorDependentStyleSheets.FirstOrDefault(
            name => string.Equals(name, MonochromeStyleSheetName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? PlotStyleResolution.Unavailable("이 도면/환경에서 monochrome.ctb를 사용할 수 없습니다.")
            : PlotStyleResolution.Resolved(match);
    }
}
