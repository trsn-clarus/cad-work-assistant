namespace CADWorkAssistant.Core.Cad;

/// <summary>ConnectionManager가 관찰한 사건. <see cref="CadConnectionStateEvaluator"/>의 입력.</summary>
public enum ConnectionSignal
{
    NoAutoCadProcessFound,
    ProcessFoundPluginUnreachable,
    MultipleInstancesAwaitingSelection,
    ConnectAttemptStarted,
    ConnectSucceeded,
    HeartbeatSucceeded,
    HeartbeatFailed,
    ManualDisconnect,
    Faulted
}
