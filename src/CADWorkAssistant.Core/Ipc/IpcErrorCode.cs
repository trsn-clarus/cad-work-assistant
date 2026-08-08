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
    InternalError
}
