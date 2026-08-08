namespace CADWorkAssistant.Core.Cad;

/// <summary>GetApplicationInfo IPC 응답 payload (§18).</summary>
public sealed class AutoCadInstanceInfo
{
    public AutoCadInstanceInfo(
        string product,
        string version,
        int processId,
        string pluginVersion,
        int protocolVersion,
        bool isSimulated = false)
    {
        Product = product;
        Version = version;
        ProcessId = processId;
        PluginVersion = pluginVersion;
        ProtocolVersion = protocolVersion;
        IsSimulated = isSimulated;
    }

    /// <summary>예: "AutoCAD 2024". 정확한 마케팅 연도를 확인할 수 없으면 원본 버전 문자열을 사용한다.</summary>
    public string Product { get; }

    /// <summary>AutoCAD 내부 버전 문자열 (예: "24.3.119.0").</summary>
    public string Version { get; }

    public int ProcessId { get; }

    public string PluginVersion { get; }

    public int ProtocolVersion { get; }

    /// <summary>true면 실제 AutoCAD가 아니라 FakeAutoCAD(Simulation Mode)에 연결된 것이다.
    /// Desktop UI는 이 값으로 "SIMULATION" 배지를 표시해 사용자가 Fake 데이터를 실제 결과로
    /// 착각하지 않게 한다 (Milestone 2 §39).</summary>
    public bool IsSimulated { get; }
}
