using CADWorkAssistant.Core.Length;

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
/// 네 가지 결과를 하나의 타입으로 감싼다 (§19 - 취소는 오류가 아니다).
/// </summary>
internal sealed class SelectionOutcome
{
    private SelectionOutcome(SelectionOutcomeKind kind, LengthSelectionResponse? response, string? errorMessage)
    {
        Kind = kind;
        Response = response;
        ErrorMessage = errorMessage;
    }

    public SelectionOutcomeKind Kind { get; }

    public LengthSelectionResponse? Response { get; }

    public string? ErrorMessage { get; }

    public static SelectionOutcome Selected(LengthSelectionResponse response) => new(SelectionOutcomeKind.Selected, response, null);

    public static SelectionOutcome Cancelled() => new(SelectionOutcomeKind.Cancelled, null, null);

    public static SelectionOutcome NoActiveDocument() => new(SelectionOutcomeKind.NoActiveDocument, null, null);

    public static SelectionOutcome Error(string message) => new(SelectionOutcomeKind.Error, null, message);
}
