using System.Collections.Generic;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>`IsolateObjects` IPC 요청 payload - 이 Handle들만 보이게 하고 나머지는 숨긴다 (§32).</summary>
public sealed class IsolateObjectsRequest
{
    public IsolateObjectsRequest(IReadOnlyList<string> handles)
    {
        Handles = handles;
    }

    public IReadOnlyList<string> Handles { get; }
}
