using System.Collections.Generic;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Length;

/// <summary>
/// `SelectLengthObjects` IPC 응답 payload. 집계는 여기서 하지 않는다 - AutoCAD Plugin은 원본 데이터만
/// 주고, 합산/변환/포맷팅은 Core의 <see cref="LengthAggregationService"/>가 담당한다 (§25, 테스트 가능성).
/// </summary>
public sealed class LengthSelectionResponse
{
    public LengthSelectionResponse(
        IReadOnlyList<CadLengthObjectDto> objects,
        IReadOnlyList<string> excludedObjectTypeNames,
        DrawingUnit unit)
    {
        Objects = objects;
        ExcludedObjectTypeNames = excludedObjectTypeNames;
        Unit = unit;
    }

    public IReadOnlyList<CadLengthObjectDto> Objects { get; }

    /// <summary>지원하지 않아 계산에서 제외된 객체의 AutoCAD 타입 이름 (예: "Hatch") (§18).</summary>
    public IReadOnlyList<string> ExcludedObjectTypeNames { get; }

    public DrawingUnit Unit { get; }
}
