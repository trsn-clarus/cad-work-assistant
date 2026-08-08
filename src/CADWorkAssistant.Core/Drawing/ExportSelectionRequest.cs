using System.Collections.Generic;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// `ExportSelection` IPC 요청 payload. TargetFilePath는 Desktop이 native SaveFileDialog(§57)로
/// 이미 확정한 절대 경로다 - 파일명 제안/살균/덮어쓰기 확인은 전부 Desktop+Core에서 끝난 뒤 여기로
/// 넘어온다. AutoCAD Plugin은 "이 Handle들을 이 경로에 WBLOCK으로 저장" 그 자체만 담당한다.
/// </summary>
public sealed class ExportSelectionRequest
{
    public ExportSelectionRequest(IReadOnlyList<string> handles, string targetFilePath)
    {
        Handles = handles;
        TargetFilePath = targetFilePath;
    }

    public IReadOnlyList<string> Handles { get; }

    public string TargetFilePath { get; }
}
