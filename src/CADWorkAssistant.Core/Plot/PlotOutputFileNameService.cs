using System;
using System.IO;
using CADWorkAssistant.Core.Drawing;

namespace CADWorkAssistant.Core.Plot;

/// <summary>
/// Milestone 11 §65-68 - 도면 PDF 제안 파일명. 새 sanitizer를 만들지 않고 Milestone 5의
/// <see cref="ExportFileNameService.Sanitize"/>를 그대로 재사용한다(§67).
/// </summary>
public static class PlotOutputFileNameService
{
    public static string SuggestFileName(
        string originalDrawingFileName,
        CadPlotScope scope,
        string? layoutName,
        CadPaperSize paperSize,
        CadPlotColorMode colorMode,
        DateTime date)
    {
        var baseName = ExportFileNameService.Sanitize(Path.GetFileNameWithoutExtension(originalDrawingFileName) ?? string.Empty);
        var colorText = colorMode == CadPlotColorMode.Monochrome ? "흑백" : "컬러";
        var dateText = date.ToString("yyyyMMdd");

        var scopeText = scope == CadPlotScope.Window
            ? "영역출력"
            : ExportFileNameService.Sanitize(string.IsNullOrWhiteSpace(layoutName) ? "Layout" : layoutName!);

        return $"{baseName}_{scopeText}_{paperSize.Name}_{colorText}_{dateText}.pdf";
    }
}
