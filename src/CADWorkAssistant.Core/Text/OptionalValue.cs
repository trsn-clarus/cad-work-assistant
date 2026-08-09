namespace CADWorkAssistant.Core.Text;

/// <summary>
/// Milestone 12 §25 - "null = 변경 안 함"과 "null = 값을 지운다"를 구분하지 못하는 모호함을 없앤다.
/// <see cref="HasValue"/>가 false면 이 속성은 아예 건드리지 않는다는 뜻이고, true면 <see cref="Value"/>가
/// 새 값이다 - 두 상태를 하나의 nullable로 뭉개지 않는다.
/// </summary>
public sealed class OptionalValue<T>
{
    public OptionalValue(bool hasValue, T? value)
    {
        HasValue = hasValue;
        Value = value;
    }

    public bool HasValue { get; }

    public T? Value { get; }

    public static OptionalValue<T> None() => new(false, default);

    public static OptionalValue<T> Some(T value) => new(true, value);
}
