using System.Collections.Generic;

namespace CADWorkAssistant.Core.Text;

/// <summary>`UpdateTextObjects` IPC 요청 (Milestone 12 §91). 단일/복수 선택 둘 다 같은 요청으로
/// 처리한다 - 단일 선택도 "Handle 1개짜리 batch"일 뿐이다.</summary>
public sealed class UpdateTextObjectsRequest
{
    public UpdateTextObjectsRequest(IReadOnlyList<string> handles, TextUpdatePatch patch)
    {
        Handles = handles;
        Patch = patch;
    }

    public IReadOnlyList<string> Handles { get; }

    public TextUpdatePatch Patch { get; }
}
