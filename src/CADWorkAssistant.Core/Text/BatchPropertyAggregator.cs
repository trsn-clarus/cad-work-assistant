using System;
using System.Collections.Generic;

namespace CADWorkAssistant.Core.Text;

/// <summary>
/// Milestone 12 §13, §96 - 선택된 여러 문자 객체에서 속성 하나를 뽑아 Uniform/Mixed를 판정하는 순수
/// 로직. 높이/Layer/색상마다 따로 메서드를 만들지 않고 selector 하나로 공유한다 - 세 속성 모두 같은
/// "전부 같으면 Uniform, 하나라도 다르면 Mixed" 규칙이기 때문이다.
/// </summary>
public static class BatchPropertyAggregator
{
    public static BatchPropertyState<T> Aggregate<T>(
        IReadOnlyList<CadTextObjectDto> objects,
        Func<CadTextObjectDto, T> selector,
        IEqualityComparer<T>? comparer = null)
    {
        if (objects.Count == 0)
        {
            return BatchPropertyState<T>.Empty();
        }

        var effectiveComparer = comparer ?? EqualityComparer<T>.Default;
        var first = selector(objects[0]);

        for (var i = 1; i < objects.Count; i++)
        {
            if (!effectiveComparer.Equals(selector(objects[i]), first))
            {
                return BatchPropertyState<T>.Mixed();
            }
        }

        return BatchPropertyState<T>.Uniform(first);
    }
}
