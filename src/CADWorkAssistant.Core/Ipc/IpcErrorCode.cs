namespace CADWorkAssistant.Core.Ipc;

public enum IpcErrorCode
{
    PluginUnavailable,
    InvalidRequest,
    UnsupportedProtocol,
    AutoCadUnavailable,
    NoActiveDocument,
    ApiExecutionFailed,
    Timeout,
    Cancelled,
    InternalError,

    /// <summary>사용자가 AutoCAD에서 Esc로 선택을 취소했다. 실패가 아니라 정상적인 사용자 조작이다 (§19).</summary>
    SelectionCancelled
}
