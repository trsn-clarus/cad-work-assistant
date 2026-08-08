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
        string? measurementSource = null,
        string? calculationMetadataJson = null)
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
        CalculationMetadataJson = calculationMetadataJson;
    }

    public string Id { get; }

    /// <summary>어느 프로젝트 소속인지 (Milestone 6) - 생성 시점에는 아직 저장되지 않았을 수 있어
    /// (예: Project가 열려 있지 않을 때는 애초에 추가할 수 없다, §21-22) 영속화 시점에 채워진다.
    /// 기존 4개 측정 도구는 이 프로퍼티를 모른다 - MainWindowViewModel이 저장 직전에 채운다.</summary>
    public string ProjectId { get; set; } = string.Empty;

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

    /// <summary>Vertical Area/Parapet의 구조화된 입력값(높이/면/상부폭 등)을 JSON으로 보존한다
    /// (Milestone 6 §24-28). Length/Area는 채우지 않는다 - 구조화할 입력이 없다(선택 결과 자체가
    /// 전부다). Schema를 자유롭게 바꿀 수 있어야 해서(예: Parapet에 필드가 추가되는 경우) 별도
    /// child table 대신 JSON을 선택했다 - 지금은 이 값을 SQL로 쿼리할 필요가 없다(§28).</summary>
    public string? CalculationMetadataJson { get; }

    /// <summary>사용자가 자유롭게 붙이는 메모 - 계산 결과와 달리 언제든 수정 가능하다(§57, 계산값
    /// 자체의 수동 수정은 이번 범위에서 의도적으로 제외했다 - §58).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Description을 마지막으로 고친 시각. 처음 만들어진 뒤로 한 번도 안 고쳤으면 null.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
