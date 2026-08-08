using CADWorkAssistant.Core.Drawing;

namespace CADWorkAssistant.Core.Tests.Drawing;

public class BoundsAggregatorTests
{
    [Fact]
    public void Aggregate_SingleObject_ReturnsSameBounds()
    {
        var bounds = new CadBoundsDto(0, 0, 0, 10, 20, 0);

        var result = BoundsAggregator.Aggregate(new[] { bounds });

        Assert.NotNull(result);
        Assert.Equal(0, result!.MinX);
        Assert.Equal(10, result.MaxX);
        Assert.Equal(20, result.MaxY);
    }

    [Fact]
    public void Aggregate_MultipleObjects_UnionsExtents()
    {
        var a = new CadBoundsDto(0, 0, 0, 10, 10, 0);
        var b = new CadBoundsDto(-5, 8, 0, 3, 30, 0);

        var result = BoundsAggregator.Aggregate(new[] { a, b });

        Assert.NotNull(result);
        Assert.Equal(-5, result!.MinX);
        Assert.Equal(0, result.MinY);
        Assert.Equal(10, result.MaxX);
        Assert.Equal(30, result.MaxY);
    }

    [Fact]
    public void Aggregate_NegativeCoordinates_HandledCorrectly()
    {
        var a = new CadBoundsDto(-100, -200, 0, -50, -150, 0);

        var result = BoundsAggregator.Aggregate(new[] { a });

        Assert.NotNull(result);
        Assert.Equal(-100, result!.MinX);
        Assert.Equal(-50, result.MaxX);
    }

    [Fact]
    public void Aggregate_LargeCoordinates_HandledCorrectly()
    {
        var a = new CadBoundsDto(1_000_000, 2_000_000, 0, 1_000_500, 2_000_500, 0);

        var result = BoundsAggregator.Aggregate(new[] { a });

        Assert.NotNull(result);
        Assert.Equal(1_000_500, result!.MaxX);
    }

    [Fact]
    public void Aggregate_NullAndInvalidExtentsMixedIn_AreIgnored()
    {
        var valid = new CadBoundsDto(0, 0, 0, 10, 10, 0);
        var invalid = new CadBoundsDto(double.NaN, 0, 0, double.PositiveInfinity, 10, 0);

        var result = BoundsAggregator.Aggregate(new[] { valid, null, invalid });

        Assert.NotNull(result);
        Assert.Equal(10, result!.MaxX);
    }

    [Fact]
    public void Aggregate_EmptyOrAllInvalid_ReturnsNull()
    {
        Assert.Null(BoundsAggregator.Aggregate(Array.Empty<CadBoundsDto?>()));
        Assert.Null(BoundsAggregator.Aggregate(new CadBoundsDto?[] { null, null }));
    }
}
