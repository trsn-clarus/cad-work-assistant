namespace CADWorkAssistant.Core.Models;

/// <summary>
/// Desktop ↔ AutoCAD 연결 상태. 단순 Connected/Disconnected 두 값으로는
/// "AutoCAD는 실행 중인데 Plugin이 없는" 상태를 표현할 수 없어서 세분화했다 (docs/AUTOCAD_INTEGRATION.md §5, Milestone 1 §7).
/// </summary>
public enum CadConnectionState
{
    /// <summary>실행 중인 acad.exe가 하나도 없다. 정상 상태이며 오류가 아니다 (§25).</summary>
    NoAutoCadProcess,

    /// <summary>acad.exe는 있지만 아직 어떤 Instance에도 연결을 시도하지 않았다 (여러 개 중 선택 대기 등).</summary>
    ProcessDetected,

    /// <summary>acad.exe는 있지만 해당 Instance에 CADWorkAssistant.AutoCAD Plugin이 로드되어 있지 않다.</summary>
    PluginUnavailable,

    Connecting,

    Connected,

    /// <summary>한 번 연결된 뒤 Heartbeat가 실패해서 재연결을 시도하는 중이다.</summary>
    Reconnecting,

    /// <summary>재연결 시도까지 실패했거나 사용자가 명시적으로 연결을 끊었다.</summary>
    Disconnected,

    /// <summary>예상하지 못한 오류로 연결 로직 자체가 실패했다.</summary>
    Faulted
}
