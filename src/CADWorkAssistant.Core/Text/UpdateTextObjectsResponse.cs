using System.Collections.Generic;

namespace CADWorkAssistant.Core.Text;

/// <summary>`UpdateTextObjects` IPC 응답 (Milestone 12 §56). 수정된 객체를 그대로 돌려줘 Desktop이
/// 다시 SelectTextObjects를 부르지 않고 화면을 갱신할 수 있게 한다. 실패하면(Handle 유효성/Locked
/// Layer 등) 이 응답 대신 IpcError로 온다 - all-or-nothing이므로 부분 성공 개념이 없다(§53).</summary>
public sealed class UpdateTextObjectsResponse
{
    public UpdateTextObjectsResponse(IReadOnlyList<CadTextObjectDto> updatedObjects)
    {
        UpdatedObjects = updatedObjects;
    }

    public IReadOnlyList<CadTextObjectDto> UpdatedObjects { get; }

    public int UpdatedCount => UpdatedObjects.Count;
}
