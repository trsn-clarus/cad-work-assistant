namespace CADWorkAssistant.Core.Ipc;

/// <summary>한 Handler 실행 결과. 성공이면 Payload, 실패면 Error 중 하나만 채워진다.</summary>
public sealed class IpcHandlerResult
{
    private IpcHandlerResult(bool success, object? payload, IpcError? error)
    {
        Success = success;
        Payload = payload;
        Error = error;
    }

    public static IpcHandlerResult Ok(object? payload = null) => new(true, payload, null);

    public static IpcHandlerResult Fail(IpcError error) => new(false, null, error);

    public bool Success { get; }

    public object? Payload { get; }

    public IpcError? Error { get; }
}
