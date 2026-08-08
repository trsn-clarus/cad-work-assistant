namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Length의 LengthWorkflowState와 같은 역할이지만, Area는 "선택은 됐는데 일부만 유효"한 경우를
/// 명확히 구분해야 한다 (§11 PartialSuccess, §46). NoValidObjects는 "선택은 했지만 전부 무효"인
/// 경우로, "아예 선택하지 않음"(EmptySelection)과 메시지가 달라야 해서 별도로 둔다.
/// </summary>
public enum AreaWorkflowState
{
    Idle,
    AwaitingSelection,
    Success,
    PartialSuccess,
    NoValidObjects,
    Cancelled,
    EmptySelection,
    Error
}
