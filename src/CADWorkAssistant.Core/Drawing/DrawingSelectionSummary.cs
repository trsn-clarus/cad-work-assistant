using System.Collections.Generic;
using System.Linq;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>객체 타입 하나와 그 개수 (§16, "Polyline 42").</summary>
public sealed class ObjectTypeCount
{
    public ObjectTypeCount(string objectType, int count)
    {
        ObjectType = objectType;
        Count = count;
    }

    public string ObjectType { get; }

    public int Count { get; }
}

/// <summary>Layer 하나와 그 위에 있는 선택 객체 개수 (§17, "A-WALL 53").</summary>
public sealed class SelectionLayerCount
{
    public SelectionLayerCount(string layerName, int count)
    {
        LayerName = layerName;
        Count = count;
    }

    public string LayerName { get; }

    public int Count { get; }
}

/// <summary>
/// 선택 결과를 사람이 읽을 수 있는 요약으로 만든다 - 타입별/Layer별 개수, 합산 Bounds
/// (Milestone 5 §16-17, §66). AutoCAD Plugin은 원본 객체 목록만 주고 이 집계는 Core에서
/// 한다(테스트 가능성, 기존 Length/Area의 AggregationService와 같은 원칙).
/// </summary>
public static class DrawingSelectionSummary
{
    /// <summary>개수 내림차순, 동률이면 이름 오름차순 - 화면에 표시할 때 항상 같은 순서가 나오게 한다.</summary>
    public static IReadOnlyList<ObjectTypeCount> SummarizeByType(IReadOnlyList<CadSelectedObjectDto> objects) =>
        objects
            .GroupBy(o => o.ObjectType)
            .Select(g => new ObjectTypeCount(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.ObjectType)
            .ToList();

    public static IReadOnlyList<SelectionLayerCount> SummarizeByLayer(IReadOnlyList<CadSelectedObjectDto> objects) =>
        objects
            .GroupBy(o => o.LayerName)
            .Select(g => new SelectionLayerCount(g.Key, g.Count()))
            .OrderByDescending(l => l.Count)
            .ThenBy(l => l.LayerName)
            .ToList();

    public static CadBoundsDto? AggregateBounds(IReadOnlyList<CadSelectedObjectDto> objects) =>
        BoundsAggregator.Aggregate(objects.Select(o => o.Bounds));
}
