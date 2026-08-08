namespace CADWorkAssistant.Core.Ipc;

/// <summary>
/// 사용자에게 보여줄 수 있는 Message와, 로그에만 남기는 TechnicalDetail(Stack Trace 등)을 분리한다 (§24, §31).
/// </summary>
public sealed class IpcError
{
    public IpcError(IpcErrorCode code, string message, string? technicalDetail = null)
    {
        Code = code;
        Message = message;
        TechnicalDetail = technicalDetail;
    }

    public IpcErrorCode Code { get; }

    public string Message { get; }

    public string? TechnicalDetail { get; }
}
