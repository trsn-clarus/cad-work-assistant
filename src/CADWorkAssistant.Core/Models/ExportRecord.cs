using System;

namespace CADWorkAssistant.Core.Models;

/// <summary>Milestone 5의 WBLOCK Export, Milestone 9의 Excel Export 결과를 프로젝트 이력에 남긴다
/// (§39, §79-81). 실제 파일은 DB에 넣지 않는다(§40) - 경로/개수 같은 metadata만 저장한다.</summary>
public sealed class ExportRecord
{
    public ExportRecord(
        string id,
        string projectId,
        string? sourceDrawing,
        string targetFile,
        int objectCount,
        string? description,
        DateTimeOffset createdAt,
        string exportType = ExportTypes.DwgSelection)
    {
        Id = id;
        ProjectId = projectId;
        SourceDrawing = sourceDrawing;
        TargetFile = targetFile;
        ObjectCount = objectCount;
        Description = description;
        CreatedAt = createdAt;
        ExportType = exportType;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public string? SourceDrawing { get; }

    public string TargetFile { get; }

    public int ObjectCount { get; }

    public string? Description { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>"DwgSelection"(Milestone 5 WBLOCK) 또는 "ExcelQuantity"(Milestone 9) - 새 Export
    /// 종류가 생기면 <see cref="ExportTypes"/>에 상수만 추가한다(§80-81).</summary>
    public string ExportType { get; }
}

public static class ExportTypes
{
    public const string DwgSelection = "DwgSelection";
    public const string ExcelQuantity = "ExcelQuantity";
}
