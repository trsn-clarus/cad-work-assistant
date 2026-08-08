namespace CADWorkAssistant.Core.Cad;

/// <summary>
/// Desktop의 Discovery 단계에서 찾은 실행 중인 AutoCAD 프로세스 하나. IPC로 오가지 않는, Desktop 내부용 값이다 (§21).
/// </summary>
public sealed class AutoCadInstanceCandidate
{
    public AutoCadInstanceCandidate(int processId, bool pluginReachable, string? activeDrawingName)
    {
        ProcessId = processId;
        PluginReachable = pluginReachable;
        ActiveDrawingName = activeDrawingName;
    }

    public int ProcessId { get; }

    /// <summary>이 PID의 Named Pipe에 연결해서 Ping에 응답을 받았는지 여부.</summary>
    public bool PluginReachable { get; }

    /// <summary>선택 UI에 표시할 용도. Plugin이 없으면 null.</summary>
    public string? ActiveDrawingName { get; }
}
