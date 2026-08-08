using System;
using System.Collections.Generic;

namespace CADWorkAssistant.Core.Models;

/// <summary>
/// Vertical Area/Parapet의 "최근 측정값 사용"이 참조하는 마지막 성공한 측정값을 프로젝트 단위로
/// 기록한다(§30). 현재 세션의 `LengthWorkflowViewModel.LastResult`(메모리)와는 별개 - 이 테이블은
/// DB에 실제로 남지만, 앱 재시작 후 "최근 측정값 사용" 라디오를 이 값으로 자동 복구하는 것은
/// 이번 범위에 포함하지 않았다(§92는 "구현한다면"이라는 조건부 표현 - PERSISTENCE.md에 스코프
/// 결정 기록). Type마다 마지막 값 하나만 의미가 있어 프로젝트+타입 조합으로 upsert한다.
/// </summary>
public sealed class RecentMeasurement
{
    public RecentMeasurement(
        string id,
        string projectId,
        string measurementType,
        decimal value,
        string unit,
        string? sourceDrawing,
        IReadOnlyList<string>? objectHandles,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        MeasurementType = measurementType;
        Value = value;
        Unit = unit;
        SourceDrawing = sourceDrawing;
        ObjectHandles = objectHandles ?? Array.Empty<string>();
        CreatedAt = createdAt;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public string MeasurementType { get; }

    public decimal Value { get; }

    public string Unit { get; }

    public string? SourceDrawing { get; }

    public IReadOnlyList<string> ObjectHandles { get; }

    public DateTimeOffset CreatedAt { get; }
}
