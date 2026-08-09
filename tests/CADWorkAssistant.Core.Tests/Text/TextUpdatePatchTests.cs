using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Core.Tests.Text;

public class TextUpdatePatchTests
{
    [Fact]
    public void Empty_HasAnyChange_IsFalse()
    {
        var patch = TextUpdatePatch.Empty();

        Assert.False(patch.HasAnyChange);
    }

    [Fact]
    public void HeightOnly_HasAnyChange_IsTrue()
    {
        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(),
            OptionalValue<double>.Some(300),
            OptionalValue<string>.None(),
            OptionalValue<CadColorDto>.None());

        Assert.True(patch.HasAnyChange);
        Assert.False(patch.Content.HasValue);
        Assert.True(patch.Height.HasValue);
        Assert.Equal(300, patch.Height.Value);
    }

    [Fact]
    public void ColorOnly_LeavesOtherPropertiesUnset()
    {
        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(),
            OptionalValue<double>.None(),
            OptionalValue<string>.None(),
            OptionalValue<CadColorDto>.Some(CadColorPalette.ByLayer));

        Assert.True(patch.HasAnyChange);
        Assert.False(patch.Height.HasValue);
        Assert.False(patch.LayerName.HasValue);
        Assert.True(patch.Color.HasValue);
    }
}
