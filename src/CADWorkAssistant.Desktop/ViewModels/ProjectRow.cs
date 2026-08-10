using System;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>Milestone 13 - Projects Workspace 목록의 한 행. Project 자체는 변경 통지가 없는 단순
/// 모델이라, 목록/검색/정렬에 필요한 파생 값(DrawingCount/IsCurrent)을 이 Row가 계산해 붙인다
/// (QuantityHistoryRow가 QuantityRecord를 감싸는 것과 같은 패턴).</summary>
public sealed class ProjectRow
{
    public ProjectRow(Project source, int drawingCount, bool isCurrent)
    {
        Source = source;
        DrawingCount = drawingCount;
        IsCurrent = isCurrent;
    }

    public Project Source { get; }

    public string Id => Source.Id;

    public string Name => Source.Name;

    public string? Client => Source.Client;

    public string? Site => Source.Site;

    public DateTimeOffset CreatedAt => Source.CreatedAt;

    public DateTimeOffset LastOpenedAt => Source.LastOpenedAt;

    public int DrawingCount { get; }

    /// <summary>§13 - 색상만으로 구분하지 않는다(연결 상태 glyph와 같은 원칙, Milestone 4.5 §67-68).</summary>
    public bool IsCurrent { get; }

    public string CurrentGlyph => IsCurrent ? "●" : string.Empty;
}
