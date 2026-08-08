using System.Collections.Generic;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Area;

/// <summary>
/// `SelectAreaObjects` IPC 응답 payload. Length의 LengthSelectionResponse와 같은 형태를 유지한다 (§5) -
/// AutoCAD Plugin은 원본 데이터만 주고, 분류/합산/변환/포맷팅은 Core의 <see cref="AreaAggregationService"/>가
/// 담당한다 (테스트 가능성).
/// </summary>
public sealed class AreaSelectionResponse
{
    public AreaSelectionResponse(
        IReadOnlyList<CadAreaObjectDto> objects,
        IReadOnlyList<string> excludedObjectTypeNames,
        DrawingUnit unit)
    {
        Objects = objects;
        ExcludedObjectTypeNames = excludedObjectTypeNames;
        Unit = unit;
    }

    /// <summary>면적 산출이 가능한 Geometry 종류(Polyline/Circle/Ellipse/Region)로 선택된 객체 전부 -
    /// 열려 있어 제외될 것들도 포함한다. "몇 개가 열려 있었는지" UI가 알아야 하기 때문이다 (§16).</summary>
    public IReadOnlyList<CadAreaObjectDto> Objects { get; }

    /// <summary>면적 산출 Geometry 종류 자체가 아닌 객체의 AutoCAD 타입 이름 (예: "Hatch", "Line") (§42).</summary>
    public IReadOnlyList<string> ExcludedObjectTypeNames { get; }

    public DrawingUnit Unit { get; }
}
