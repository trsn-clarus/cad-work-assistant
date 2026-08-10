using System.IO;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>Milestone 13 §32-37 - Project Detail의 OUTPUTS 한 행(DWG/Excel/PDF 보고서/도면 PDF를
/// 전부 합친 통합 Output History). ExportRecord.ExportType 원문을 그대로 노출하지 않는다(§34).</summary>
public sealed class OutputRecordRow
{
    public OutputRecordRow(ExportRecord source, bool fileExists)
    {
        Source = source;
        FileExists = fileExists;
    }

    public ExportRecord Source { get; }

    public string TypeLabel => ExportTypeDisplay.Label(Source.ExportType);

    public string FileName => Path.GetFileName(Source.TargetFile);

    public bool FileExists { get; }

    public string StatusLabel => FileExists ? "정상" : "파일 없음";
}
