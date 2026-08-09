namespace CADWorkAssistant.Core.Text;

public enum BatchPropertyKind
{
    /// <summary>대상 객체가 없다(선택이 비었다).</summary>
    Empty,

    /// <summary>선택된 모든 객체가 같은 값을 갖는다 - <see cref="BatchPropertyState{T}.Value"/>가 실제 값.</summary>
    Uniform,

    /// <summary>선택된 객체마다 값이 다르다(§13, "혼합").</summary>
    Mixed
}

/// <summary>
/// Milestone 12 §13 - 여러 객체를 선택했을 때 속성 하나를 어떻게 보여줄지 나타낸다. WPF에
/// `"혼합"`이라는 문자열을 domain value 자리에 억지로 끼워 넣지 않기 위한 타입이다(§13) - Inspector는
/// <see cref="Kind"/>로 분기하고, 실제 값은 Uniform일 때만 <see cref="Value"/>에서 꺼낸다.
/// </summary>
public sealed class BatchPropertyState<T>
{
    private BatchPropertyState(BatchPropertyKind kind, T? value)
    {
        Kind = kind;
        Value = value;
    }

    public BatchPropertyKind Kind { get; }

    /// <summary><see cref="Kind"/>가 Uniform일 때만 의미가 있다.</summary>
    public T? Value { get; }

    public bool IsMixed => Kind == BatchPropertyKind.Mixed;

    public static BatchPropertyState<T> Empty() => new(BatchPropertyKind.Empty, default);

    public static BatchPropertyState<T> Uniform(T value) => new(BatchPropertyKind.Uniform, value);

    public static BatchPropertyState<T> Mixed() => new(BatchPropertyKind.Mixed, default);
}
