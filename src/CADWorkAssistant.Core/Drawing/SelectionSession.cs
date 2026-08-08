using System;
using System.Collections.Generic;
using System.Linq;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// 한 번의 선택 결과를 담아 재사용한다 (§13) - "선택 → 확인 → 격리 → Export"를 매 단계마다 AutoCAD에서
/// 다시 선택하지 않고 이어서 수행할 수 있게 한다. Isolate/Export 둘 다 이 세션의 <see cref="Handles"/>만
/// 있으면 되므로, Desktop이 세션을 들고 있는 동안은 추가 IPC Selection round-trip이 필요 없다.
/// </summary>
public sealed class SelectionSession
{
    public SelectionSession(
        string id,
        string? drawingName,
        IReadOnlyList<CadSelectedObjectDto> objects,
        DateTimeOffset createdAt)
    {
        Id = id;
        DrawingName = drawingName;
        Objects = objects;
        CreatedAt = createdAt;
        TypeCounts = DrawingSelectionSummary.SummarizeByType(objects);
        LayerCounts = DrawingSelectionSummary.SummarizeByLayer(objects);
        Bounds = DrawingSelectionSummary.AggregateBounds(objects);
    }

    public string Id { get; }

    public string? DrawingName { get; }

    public IReadOnlyList<CadSelectedObjectDto> Objects { get; }

    public IReadOnlyList<ObjectTypeCount> TypeCounts { get; }

    public IReadOnlyList<SelectionLayerCount> LayerCounts { get; }

    public CadBoundsDto? Bounds { get; }

    public DateTimeOffset CreatedAt { get; }

    public int ObjectCount => Objects.Count;

    public IReadOnlyList<string> Handles => Objects.Select(o => o.Handle).ToList();

    public static SelectionSession Create(string? drawingName, IReadOnlyList<CadSelectedObjectDto> objects, DateTimeOffset createdAt) =>
        new("SEL-" + createdAt.ToUnixTimeMilliseconds(), drawingName, objects, createdAt);
}
