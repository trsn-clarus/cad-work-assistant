using System;
using System.Collections.Generic;
using System.Linq;

namespace CADWorkAssistant.Core.Plot;

/// <summary>
/// Milestone 11 §17-18 - PDF 출력 가능한 장치 중 하나를 자동으로 고른다. "DWG To PDF.pc3"가 흔히
/// 존재하지만 설치 환경마다 무조건 있다고 가정하지 않는다(§16) - 있으면 우선하고, 없으면 실제
/// 조회된 목록에서 PDF-capable로 표시된 첫 장치를 쓴다. 아무것도 없으면 null - 그 경우 Desktop이
/// "PDF Plotter를 찾지 못했습니다"로 안내한다(§77).
/// </summary>
public static class PlotPdfDeviceSelector
{
    public const string PreferredDeviceName = "DWG To PDF.pc3";

    public static CadPlotDeviceDto? SelectBest(IReadOnlyList<CadPlotDeviceDto> devices)
    {
        var pdfCapable = devices.Where(d => d.IsPdfCapable).ToList();
        if (pdfCapable.Count == 0)
        {
            return null;
        }

        var preferred = pdfCapable.FirstOrDefault(
            d => string.Equals(d.Name, PreferredDeviceName, StringComparison.OrdinalIgnoreCase));

        return preferred ?? pdfCapable[0];
    }
}
