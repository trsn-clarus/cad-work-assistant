namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

internal enum SelectionOutcomeKind
{
    Selected,
    Cancelled,
    NoActiveDocument,
    Error
}

/// <summary>
/// AutoCadDispatcher.InvokeInCommandContextAsync는 T 하나만 돌려줄 수 있어서, "선택됨/취소/문서없음/오류"
/// 네 가지 결과를 하나의 타입으로 감싼다 (Milestone 2 §19 - 취소는 오류가 아니다). Length/Area 등
/// 인터랙티브 Selection이 필요한 모든 Handler가 공유한다 (Milestone 3 §5 - 같은 패턴을 유지한다).
/// </summary>
internal sealed class SelectionOutcome<TResponse>
{
    private SelectionOutcome(SelectionOutcomeKind kind, TResponse? response, string? errorMessage)
    {
        Kind = kind;
        Response = response;
        ErrorMessage = errorMessage;
    }

    public SelectionOutcomeKind Kind { get; }

    public TResponse? Response { get; }

    public string? ErrorMessage { get; }

    public static SelectionOutcome<TResponse> Selected(TResponse response) => new(SelectionOutcomeKind.Selected, response, null);

    public static SelectionOutcome<TResponse> Cancelled() => new(SelectionOutcomeKind.Cancelled, default, null);

    public static SelectionOutcome<TResponse> NoActiveDocument() => new(SelectionOutcomeKind.NoActiveDocument, default, null);

    public static SelectionOutcome<TResponse> Error(string message) => new(SelectionOutcomeKind.Error, default, message);
}
