using System;
using System.Collections.Generic;
using System.Linq;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Area;

/// <summary>
/// 화면에 보여줄 집계 결과. 저장된 산출내역(QuantityRecord)과는 책임을 분리한다 - Length의
/// LengthMeasurementResult와 같은 구조다 (§9, §5).
/// </summary>
public sealed class AreaMeasurementResult
{
    public AreaMeasurementResult(
        string? drawingName,
        IReadOnlyList<AreaMeasurementItem> items,
        double rawTotalArea,
        DrawingUnit sourceUnit,
        double? displayValueSquareMeters,
        DateTimeOffset createdAt)
    {
        DrawingName = drawingName;
        Items = items;
        RawTotalArea = rawTotalArea;
        SourceUnit = sourceUnit;
        DisplayValueSquareMeters = displayValueSquareMeters;
        CreatedAt = createdAt;
    }

    public string? DrawingName { get; }

    /// <summary>선택된 객체 전부 - 유효/열림/미지원/비정상형상 전부 포함한다 (§9).</summary>
    public IReadOnlyList<AreaMeasurementItem> Items { get; }

    public int SelectedCount => Items.Count;

    public int SupportedCount => Items.Count(i => i.Status == AreaObjectStatus.Valid);

    public int ExcludedCount => SelectedCount - SupportedCount;

    /// <summary>Status가 Valid인 항목들의 합. 도면 원본 단위(제곱) 기준이다.</summary>
    public double RawTotalArea { get; }

    public DrawingUnit SourceUnit { get; }

    /// <summary>SourceUnit이 Unitless/Other라 환산할 수 없으면 null - 0으로 보여주지 않는다 (§24).</summary>
    public double? DisplayValueSquareMeters { get; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<AreaMeasurementItem> OpenItems => Filter(AreaObjectStatus.Open);

    public IReadOnlyList<AreaMeasurementItem> UnsupportedItems => Filter(AreaObjectStatus.Unsupported);

    public IReadOnlyList<AreaMeasurementItem> InvalidGeometryItems => Filter(AreaObjectStatus.InvalidGeometry);

    private IReadOnlyList<AreaMeasurementItem> Filter(AreaObjectStatus status) =>
        Items.Where(i => i.Status == status).ToList();
}
