using System;

namespace CADWorkAssistant.Core.Models;

/// <summary>
/// 사용자에게 의미 있는 작업 기록 - Serilog(진단 로그)와는 다르다(§34). "Navigation clicked"/
/// "TextBox focused" 같은 건 여기 안 들어간다(§32) - 산출내역 추가, 프로젝트 생성/열기, Export
/// 완료처럼 사용자가 "내가 방금 뭘 했지"를 되짚어볼 만한 것만 기록한다. Milestone 4.5의
/// OperationLogEntry(Timestamp/Level/Message/Detail)를 대체한다 - Level은 심각도였지만
/// ActivityType은 어떤 종류의 작업인지를 나타내는 분류값이라 의미가 다르다.
/// </summary>
public sealed class ActivityRecord
{
    public ActivityRecord(
        string id,
        string projectId,
        string activityType,
        string title,
        string? description,
        DateTimeOffset createdAt,
        string? metadataJson = null)
    {
        Id = id;
        ProjectId = projectId;
        ActivityType = activityType;
        Title = title;
        Description = description;
        CreatedAt = createdAt;
        MetadataJson = metadataJson;
    }

    public string Id { get; }

    public string ProjectId { get; }

    /// <summary>"QuantityAdded"/"ProjectOpened"/"ExportCompleted" 등 - 화면에 그대로 노출하지 않는다
    /// (§75, Raw 이름 표시 금지). Title/Description이 사람이 읽는 문구를 담당한다.</summary>
    public string ActivityType { get; }

    /// <summary>사람이 읽는 요약 - 예: "파라펫 수량 추가".</summary>
    public string Title { get; }

    /// <summary>사람이 읽는 상세 - 예: "69.054 m²".</summary>
    public string? Description { get; }

    public DateTimeOffset CreatedAt { get; }

    public string? MetadataJson { get; }
}
