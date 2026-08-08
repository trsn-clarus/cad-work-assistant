using System;

namespace CADWorkAssistant.Core.Models;

/// <summary>Milestone 5의 WBLOCK Export 결과를 프로젝트 이력에 남긴다(§39). 실제 DWG 파일은 DB에
/// 넣지 않는다(§40) - 경로/개수 같은 metadata만 저장한다.</summary>
public sealed class ExportRecord
{
    public ExportRecord(
        string id,
        string projectId,
        string? sourceDrawing,
        string targetFile,
        int objectCount,
        string? description,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        SourceDrawing = sourceDrawing;
        TargetFile = targetFile;
        ObjectCount = objectCount;
        Description = description;
        CreatedAt = createdAt;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public string? SourceDrawing { get; }

    public string TargetFile { get; }

    public int ObjectCount { get; }

    public string? Description { get; }

    public DateTimeOffset CreatedAt { get; }
}
