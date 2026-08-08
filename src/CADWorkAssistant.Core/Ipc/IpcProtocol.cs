namespace CADWorkAssistant.Core.Ipc;

/// <summary>
/// Desktop과 AutoCAD Plugin이 공유하는 IPC 상수. 값을 바꾸면 두 프로세스를 모두 다시 빌드해야 한다.
/// </summary>
public static class IpcProtocol
{
    /// <summary>현재 프로토콜 버전. Envelope 구조나 MessageType 의미가 호환되지 않게 바뀔 때만 올린다.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Named Pipe 이름 접두사. 실제 이름은 접두사 + AutoCAD 프로세스 PID (docs/AUTOCAD_INTEGRATION.md §5).</summary>
    public const string PipeNamePrefix = "CADWorkAssistant.AutoCAD.";

    /// <summary>단일 메시지 최대 크기. 상태 조회용 메시지가 이보다 커질 이유가 없다.</summary>
    public const int MaxMessageSizeBytes = 1024 * 1024;

    public const int ConnectTimeoutMs = 1500;
    public const int RequestTimeoutMs = 3000;
    public const int HeartbeatIntervalMs = 2000;

    public static string GetPipeName(int autoCadProcessId) => PipeNamePrefix + autoCadProcessId.ToString();
}
