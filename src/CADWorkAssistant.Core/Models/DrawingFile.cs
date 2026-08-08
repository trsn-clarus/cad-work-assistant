using System;

namespace CADWorkAssistant.Core.Models;

/// <summary>
/// 프로젝트에서 사용한 DWG 참조 - 파일을 복사하지 않는다(§36), 경로만 기억한다. Milestone 4.5에서
/// 같은 이름의 모델을 삭제한 적이 있다(가짜 하드코딩 목록만 있었다) - 이번엔 실제 AutoCAD 연결 시
/// 자동 등록되는 진짜 참조다.
/// </summary>
public sealed class DrawingFile
{
    public DrawingFile(
        string id,
        string projectId,
        string fileName,
        string fullPath,
        string? drawingUnit,
        DateTimeOffset lastSeenAt,
        DateTimeOffset lastOpenedAt,
        bool isMissing = false)
    {
        Id = id;
        ProjectId = projectId;
        FileName = fileName;
        FullPath = fullPath;
        DrawingUnit = drawingUnit;
        LastSeenAt = lastSeenAt;
        LastOpenedAt = lastOpenedAt;
        IsMissing = isMissing;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public string FileName { get; set; }

    /// <summary>정규화된 절대 경로 - 같은 파일이 중복 행으로 쌓이지 않도록 이 값으로 중복을 판단한다
    /// (§97-98).</summary>
    public string FullPath { get; }

    public string? DrawingUnit { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset LastOpenedAt { get; set; }

    /// <summary>파일이 이동/삭제되어 더 이상 찾을 수 없는지 (§37) - Project 전체를 깨뜨리지 않고
    /// 이 플래그만 표시한다.</summary>
    public bool IsMissing { get; set; }
}
