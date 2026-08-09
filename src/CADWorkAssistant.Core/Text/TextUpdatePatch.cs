namespace CADWorkAssistant.Core.Text;

/// <summary>
/// Milestone 12 §24, §156-157 - 여러 문자 객체에 한 번에 적용할 변경사항. 사용자가 명시적으로 고른
/// 속성만 <see cref="OptionalValue{T}.HasValue"/>가 true다 - 나머지 속성은 그대로 보존된다(§157
/// "선택하지 않은 속성은 변경하지 않는다"). Content는 단일 선택에서만 의미가 있다(§19) - 여러 객체를
/// 선택한 상태에서는 항상 <see cref="OptionalValue{T}.None"/>이어야 한다(Desktop이 강제한다).
/// </summary>
public sealed class TextUpdatePatch
{
    public TextUpdatePatch(
        OptionalValue<string> content,
        OptionalValue<double> height,
        OptionalValue<string> layerName,
        OptionalValue<CadColorDto> color)
    {
        Content = content;
        Height = height;
        LayerName = layerName;
        Color = color;
    }

    public OptionalValue<string> Content { get; }

    public OptionalValue<double> Height { get; }

    public OptionalValue<string> LayerName { get; }

    public OptionalValue<CadColorDto> Color { get; }

    public static TextUpdatePatch Empty() => new(
        OptionalValue<string>.None(),
        OptionalValue<double>.None(),
        OptionalValue<string>.None(),
        OptionalValue<CadColorDto>.None());

    /// <summary>Apply 버튼 활성화 조건(§68) - 실제로 바꿀 속성이 하나도 없으면 false.</summary>
    public bool HasAnyChange => Content.HasValue || Height.HasValue || LayerName.HasValue || Color.HasValue;
}
