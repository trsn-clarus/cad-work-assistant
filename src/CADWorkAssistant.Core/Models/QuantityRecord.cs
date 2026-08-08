using System;
using System.Collections.Generic;

namespace CADWorkAssistant.Core.Models;

/// <summary>
/// 사용자가 "산출내역 추가"로 저장하기로 한 값. 방금 계산한 결과(Length.LengthMeasurementResult)와는
/// 다르다 - 이건 저장된 기록이다 (Milestone 2 §26).
/// </summary>
public sealed class QuantityRecord
{
    public QuantityRecord(
        string id,
        string type,
        string layer,
        int objectCount,
        decimal value,
        string unit,
        string sourceDrawing,
        DateTimeOffset createdAt,
        decimal? rawValue = null,
        string? sourceUnit = null,
        IReadOnlyList<string>? objectHandles = null,
        string? calculationExpression = null,
        string? measurementSource = null)
    {
        Id = id;
        Type = type;
        Layer = layer;
        ObjectCount = objectCount;
        Value = value;
        Unit = unit;
        SourceDrawing = sourceDrawing;
        CreatedAt = createdAt;
        RawValue = rawValue;
        SourceUnit = sourceUnit;
        ObjectHandles = objectHandles ?? Array.Empty<string>();
        CalculationExpression = calculationExpression;
        MeasurementSource = measurementSource;
    }

    public string Id { get; }
    public string Type { get; }
    public string Layer { get; }
    public int ObjectCount { get; }

    /// <summary>화면에 표시하는 변환된 값 (예: 미터).</summary>
    public decimal Value { get; }

    public string Unit { get; }
    public string SourceDrawing { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>도면 원본 단위 기준 값 - 나중에 재검산할 때 필요하다 (§28, §20).</summary>
    public decimal? RawValue { get; }

    /// <summary>RawValue의 단위 (예: "mm"). Value/Unit과는 다를 수 있다.</summary>
    public string? SourceUnit { get; }

    /// <summary>이 값을 만든 AutoCAD 객체들의 Handle - 향후 "CAD에서 원본 객체 찾기"에 쓴다 (§29).</summary>
    public IReadOnlyList<string> ObjectHandles { get; }

    /// <summary>사람이 읽을 수 있는 산식 (예: "125.331 + 81.405 + 49.205 = 255.941 m").</summary>
    public string? CalculationExpression { get; }

    /// <summary>
    /// 기준 길이가 어디서 왔는지 - "CadSelection"/"ExistingMeasurement"/"Manual"
    /// (Core.VerticalArea.MeasurementSourceType의 문자열 표현). Length/Area처럼 항상 CAD에서
    /// 직접 뽑는 값에는 없어도 되는 정보라 Length/Area는 null로 남긴다 (Milestone 4 §19, §52).
    /// </summary>
    public string? MeasurementSource { get; }
}
