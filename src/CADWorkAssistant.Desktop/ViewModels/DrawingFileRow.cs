using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>Milestone 13 §17-25 - Project Detail의 DRAWINGS 한 행. 존재 여부는 매번 실제
/// File.Exists로 확인한 값이다(§21-22) - DrawingFile.IsMissing 컬럼에 캐시된 값을 신뢰하지 않는다
/// (네트워크 경로 등에서 값이 오래될 수 있다).</summary>
public sealed class DrawingFileRow
{
    public DrawingFileRow(DrawingFile source, bool fileExists)
    {
        Source = source;
        FileExists = fileExists;
    }

    public DrawingFile Source { get; }

    public string Id => Source.Id;

    public string FileName => Source.FileName;

    public string FullPath => Source.FullPath;

    public bool FileExists { get; }

    public string StatusLabel => FileExists ? "정상" : "파일 없음";
}
