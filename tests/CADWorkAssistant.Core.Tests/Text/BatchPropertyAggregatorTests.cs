using System.Collections.Generic;
using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Core.Tests.Text;

public class BatchPropertyAggregatorTests
{
    private static CadTextObjectDto MakeText(string handle, double height, string layerName, CadColorDto color) => new(
        handle,
        CadTextEntityType.SingleLine,
        content: "content",
        plainText: "content",
        layerName: layerName,
        height: height,
        rotation: 0,
        color: color,
        textStyleName: "Standard",
        isLocked: false,
        isAnnotative: false,
        hasInlineFormatting: false);

    [Fact]
    public void Aggregate_EmptyList_ReturnsEmpty()
    {
        var result = BatchPropertyAggregator.Aggregate(System.Array.Empty<CadTextObjectDto>(), o => o.Height);

        Assert.Equal(BatchPropertyKind.Empty, result.Kind);
    }

    [Fact]
    public void Aggregate_SingleObject_ReturnsUniform()
    {
        var objects = new List<CadTextObjectDto> { MakeText("1", 250, "A-TEXT", CadColorPalette.ByLayer) };

        var result = BatchPropertyAggregator.Aggregate(objects, o => o.Height);

        Assert.Equal(BatchPropertyKind.Uniform, result.Kind);
        Assert.Equal(250, result.Value);
    }

    [Fact]
    public void Aggregate_AllSameHeight_ReturnsUniform()
    {
        var objects = new List<CadTextObjectDto>
        {
            MakeText("1", 250, "A-TEXT", CadColorPalette.ByLayer),
            MakeText("2", 250, "A-TEXT", CadColorPalette.ByLayer),
            MakeText("3", 250, "A-TEXT", CadColorPalette.ByLayer)
        };

        var result = BatchPropertyAggregator.Aggregate(objects, o => o.Height);

        Assert.Equal(BatchPropertyKind.Uniform, result.Kind);
        Assert.Equal(250, result.Value);
    }

    [Fact]
    public void Aggregate_DifferentHeights_ReturnsMixed()
    {
        var objects = new List<CadTextObjectDto>
        {
            MakeText("1", 250, "A-TEXT", CadColorPalette.ByLayer),
            MakeText("2", 300, "A-TEXT", CadColorPalette.ByLayer)
        };

        var result = BatchPropertyAggregator.Aggregate(objects, o => o.Height);

        Assert.True(result.IsMixed);
    }

    [Fact]
    public void Aggregate_DifferentLayers_ReturnsMixed()
    {
        var objects = new List<CadTextObjectDto>
        {
            MakeText("1", 250, "A-TEXT", CadColorPalette.ByLayer),
            MakeText("2", 250, "A-DIM", CadColorPalette.ByLayer)
        };

        var result = BatchPropertyAggregator.Aggregate(objects, o => o.LayerName);

        Assert.True(result.IsMixed);
    }

    [Fact]
    public void Aggregate_ColorRequiresValueEquality_NotReferenceEquality()
    {
        // CadColorDto.Equals - 다른 인스턴스라도 값이 같으면 Uniform으로 판정돼야 한다.
        var objects = new List<CadTextObjectDto>
        {
            MakeText("1", 250, "A-TEXT", new CadColorDto(CadColorMode.Aci, 1, 0, 0, 0, "Red")),
            MakeText("2", 250, "A-TEXT", new CadColorDto(CadColorMode.Aci, 1, 0, 0, 0, "Red (다른 인스턴스)"))
        };

        var result = BatchPropertyAggregator.Aggregate(objects, o => o.Color);

        Assert.Equal(BatchPropertyKind.Uniform, result.Kind);
    }

    [Fact]
    public void Aggregate_DifferentColorModes_ReturnsMixed()
    {
        var objects = new List<CadTextObjectDto>
        {
            MakeText("1", 250, "A-TEXT", CadColorPalette.ByLayer),
            MakeText("2", 250, "A-TEXT", new CadColorDto(CadColorMode.Aci, 1, 0, 0, 0, "Red"))
        };

        var result = BatchPropertyAggregator.Aggregate(objects, o => o.Color);

        Assert.True(result.IsMixed);
    }
}
