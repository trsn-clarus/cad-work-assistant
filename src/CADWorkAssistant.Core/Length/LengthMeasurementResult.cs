using System;
using System.Collections.Generic;
using CADWorkAssistant.Core.Cad;

namespace CADWorkAssistant.Core.Length;

/// <summary>
/// 화면에 보여줄 집계 결과. 저장된 산출내역(QuantityRecord)과는 책임을 분리한다 - 이건 "방금 계산한 값"이고,
/// QuantityRecord는 "사용자가 저장하기로 한 값"이다 (§26).
/// </summary>
public sealed class LengthMeasurementResult
{
    public LengthMeasurementResult(
        string? drawingName,
        IReadOnlyList<CadLengthObjectDto> objects,
        IReadOnlyList<string> excludedObjectTypeNames,
        double rawTotalLength,
        DrawingUnit sourceUnit,
        double? displayValueMeters,
        DateTimeOffset createdAt)
    {
        DrawingName = drawingName;
        Objects = objects;
        ExcludedObjectTypeNames = excludedObjectTypeNames;
        RawTotalLength = rawTotalLength;
        SourceUnit = sourceUnit;
        DisplayValueMeters = displayValueMeters;
        CreatedAt = createdAt;
    }

    public string? DrawingName { get; }

    public IReadOnlyList<CadLengthObjectDto> Objects { get; }

    public int ObjectCount => Objects.Count;

    public IReadOnlyList<string> ExcludedObjectTypeNames { get; }

    public int ExcludedCount => ExcludedObjectTypeNames.Count;

    public double RawTotalLength { get; }

    public DrawingUnit SourceUnit { get; }

    /// <summary>SourceUnit이 Unitless/Other라 환산할 수 없으면 null (§22) - 0으로 보여주지 않는다.</summary>
    public double? DisplayValueMeters { get; }

    public DateTimeOffset CreatedAt { get; }
}
