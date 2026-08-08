using CADWorkAssistant.Core.Drawing;

namespace CADWorkAssistant.Core.Tests.Drawing;

public class SelectionSessionTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-08-08T10:35:00+09:00");

    [Fact]
    public void Create_BuildsSummariesAndBoundsFromObjects()
    {
        var objects = new[]
        {
            new CadSelectedObjectDto("2A7F", "Polyline", "A-WALL", new CadBoundsDto(0, 0, 0, 10, 10, 0)),
            new CadSelectedObjectDto("2A80", "Line", "A-WALL", new CadBoundsDto(5, 5, 0, 20, 20, 0)),
        };

        var session = SelectionSession.Create("School_Roof.dwg", objects, CreatedAt);

        Assert.Equal("School_Roof.dwg", session.DrawingName);
        Assert.Equal(2, session.ObjectCount);
        Assert.Equal(new[] { "2A7F", "2A80" }, session.Handles);
        Assert.Equal(CreatedAt, session.CreatedAt);
        Assert.NotNull(session.Bounds);
        Assert.Equal(20, session.Bounds!.MaxX);
        Assert.Single(session.LayerCounts);
        Assert.Equal("A-WALL", session.LayerCounts[0].LayerName);
        Assert.Equal(2, session.LayerCounts[0].Count);
        Assert.Equal(2, session.TypeCounts.Count);
    }

    [Fact]
    public void Create_EmptySelection_HasNoBoundsAndZeroCount()
    {
        var session = SelectionSession.Create("Empty.dwg", Array.Empty<CadSelectedObjectDto>(), CreatedAt);

        Assert.Equal(0, session.ObjectCount);
        Assert.Null(session.Bounds);
        Assert.Empty(session.Handles);
    }

    [Fact]
    public void Create_DuplicateHandles_AreKeptAsIs()
    {
        // Core는 중복 Handle을 걸러내지 않는다 - AutoCAD Selection Set 자체가 중복을 주지 않는다고
        // 신뢰하고, 여기서는 받은 그대로 집계한다(§106 duplicate handles 케이스를 명시적으로 커버).
        var objects = new[]
        {
            new CadSelectedObjectDto("DUP", "Line", "A", new CadBoundsDto(0, 0, 0, 1, 1, 0)),
            new CadSelectedObjectDto("DUP", "Line", "A", new CadBoundsDto(0, 0, 0, 1, 1, 0)),
        };

        var session = SelectionSession.Create("D.dwg", objects, CreatedAt);

        Assert.Equal(2, session.ObjectCount);
        Assert.Equal(new[] { "DUP", "DUP" }, session.Handles);
    }
}
