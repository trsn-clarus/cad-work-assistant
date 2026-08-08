namespace CADWorkAssistant.Core.Parapet;

/// <summary>
/// 높이와 상부 폭은 서로 다른 이유로 동시에 무효일 수 있다 - 단일 enum으로는 "둘 다 무효"를
/// 표현할 수 없어서 별도 타입으로 둔다 (Milestone 4 §59).
/// </summary>
public sealed class ParapetValidationResult
{
    public ParapetValidationResult(bool isHeightValid, bool isTopWidthValid)
    {
        IsHeightValid = isHeightValid;
        IsTopWidthValid = isTopWidthValid;
    }

    public bool IsHeightValid { get; }

    /// <summary>상부면 미포함이면 항상 true - Width는 애초에 검사 대상이 아니다 (§38).</summary>
    public bool IsTopWidthValid { get; }

    public bool IsValid => IsHeightValid && IsTopWidthValid;
}
