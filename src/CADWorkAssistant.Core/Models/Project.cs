using System;

namespace CADWorkAssistant.Core.Models;

/// <summary>
/// Milestone 6의 핵심 Domain - 프로젝트 하나. 파일명/이름을 식별자로 쓰지 않는다(§14) - 이름은
/// 바뀔 수 있고 같은 이름의 프로젝트가 여러 개 있을 수 있다(§16), Id(GUID)만이 진짜 식별자다.
/// </summary>
public sealed class Project
{
    public Project(
        string id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset lastOpenedAt,
        string? client = null,
        string? site = null,
        string? description = null)
    {
        Id = id;
        Name = name;
        Client = client;
        Site = site;
        Description = description;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        LastOpenedAt = lastOpenedAt;
    }

    public string Id { get; }

    public string Name { get; set; }

    public string? Client { get; set; }

    public string? Site { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset LastOpenedAt { get; set; }
}
