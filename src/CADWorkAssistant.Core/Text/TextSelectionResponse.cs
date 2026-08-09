using System.Collections.Generic;

namespace CADWorkAssistant.Core.Text;

/// <summary>`SelectTextObjects` IPC 응답 (Milestone 12 §17, §77). Length/Area의 Selection 응답과
/// 같은 모양 - 지원 객체(Objects)와 제외된 타입 이름(ExcludedObjectTypeNames)을 함께 돌려준다.</summary>
public sealed class TextSelectionResponse
{
    public TextSelectionResponse(IReadOnlyList<CadTextObjectDto> objects, IReadOnlyList<string> excludedObjectTypeNames)
    {
        Objects = objects;
        ExcludedObjectTypeNames = excludedObjectTypeNames;
    }

    public IReadOnlyList<CadTextObjectDto> Objects { get; }

    /// <summary>Dimension/MLeader/Table/AttributeReference 등 문자 유사 객체를 선택에 포함했을 때
    /// 실제로 제외된 타입 이름(§8, §77) - "지원되지 않는 객체 N개 제외"에 쓴다.</summary>
    public IReadOnlyList<string> ExcludedObjectTypeNames { get; }
}
