using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Core.Tests.Models;

public class QuantityRecordTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var createdAt = DateTimeOffset.Parse("2026-08-08T10:35:00+09:00");

        var record = new QuantityRecord(
            "Q-1024",
            "Length",
            "A-WALL",
            4,
            255.941m,
            "m",
            "CWA_B1_FloorPlan.dwg",
            createdAt);

        Assert.Equal("Q-1024", record.Id);
        Assert.Equal("Length", record.Type);
        Assert.Equal("A-WALL", record.Layer);
        Assert.Equal(4, record.ObjectCount);
        Assert.Equal(255.941m, record.Value);
        Assert.Equal("m", record.Unit);
        Assert.Equal("CWA_B1_FloorPlan.dwg", record.SourceDrawing);
        Assert.Equal(createdAt, record.CreatedAt);
    }
}
