using CADWorkAssistant.Core.Drawing;

namespace CADWorkAssistant.Core.Tests.Drawing;

public class DrawingSelectionSummaryTests
{
    private static CadSelectedObjectDto Obj(string handle, string type, string layer) =>
        new(handle, type, layer, new CadBoundsDto(0, 0, 0, 1, 1, 0));

    [Fact]
    public void SummarizeByType_GroupsAndOrdersByCountDescending()
    {
        var objects = new[]
        {
            Obj("1", "Line", "A-WALL"),
            Obj("2", "Polyline", "A-WALL"),
            Obj("3", "Polyline", "A-WALL"),
            Obj("4", "Polyline", "A-FLOOR"),
        };

        var result = DrawingSelectionSummary.SummarizeByType(objects);

        Assert.Equal(2, result.Count);
        Assert.Equal("Polyline", result[0].ObjectType);
        Assert.Equal(3, result[0].Count);
        Assert.Equal("Line", result[1].ObjectType);
        Assert.Equal(1, result[1].Count);
    }

    [Fact]
    public void SummarizeByType_TiedCounts_OrderedAlphabetically()
    {
        var objects = new[] { Obj("1", "Line", "A"), Obj("2", "Arc", "A") };

        var result = DrawingSelectionSummary.SummarizeByType(objects);

        Assert.Equal("Arc", result[0].ObjectType);
        Assert.Equal("Line", result[1].ObjectType);
    }

    [Fact]
    public void SummarizeByLayer_GroupsAndOrdersByCountDescending()
    {
        var objects = new[]
        {
            Obj("1", "Line", "A-WALL"),
            Obj("2", "Line", "A-WALL"),
            Obj("3", "Line", "A-TEXT"),
        };

        var result = DrawingSelectionSummary.SummarizeByLayer(objects);

        Assert.Equal("A-WALL", result[0].LayerName);
        Assert.Equal(2, result[0].Count);
        Assert.Equal("A-TEXT", result[1].LayerName);
    }

    [Fact]
    public void AggregateBounds_UnionsAllObjectBounds()
    {
        var objects = new[]
        {
            new CadSelectedObjectDto("1", "Line", "A", new CadBoundsDto(0, 0, 0, 5, 5, 0)),
            new CadSelectedObjectDto("2", "Line", "A", new CadBoundsDto(3, -2, 0, 10, 1, 0)),
        };

        var result = DrawingSelectionSummary.AggregateBounds(objects);

        Assert.NotNull(result);
        Assert.Equal(0, result!.MinX);
        Assert.Equal(-2, result.MinY);
        Assert.Equal(10, result.MaxX);
        Assert.Equal(5, result.MaxY);
    }

    [Fact]
    public void AggregateBounds_ObjectsWithoutBounds_ReturnsNull()
    {
        var objects = new[] { new CadSelectedObjectDto("1", "BlockReference", "A", null) };

        var result = DrawingSelectionSummary.AggregateBounds(objects);

        Assert.Null(result);
    }
}
