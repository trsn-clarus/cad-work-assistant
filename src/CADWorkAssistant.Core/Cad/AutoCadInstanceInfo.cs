namespace CADWorkAssistant.Core.Cad;

/// <summary>GetApplicationInfo IPC 응답 payload (§18).</summary>
public sealed class AutoCadInstanceInfo
{
    public AutoCadInstanceInfo(string product, string version, int processId, string pluginVersion, int protocolVersion)
    {
        Product = product;
        Version = version;
        ProcessId = processId;
        PluginVersion = pluginVersion;
        ProtocolVersion = protocolVersion;
    }

    /// <summary>예: "AutoCAD 2024". 정확한 마케팅 연도를 확인할 수 없으면 원본 버전 문자열을 사용한다.</summary>
    public string Product { get; }

    /// <summary>AutoCAD 내부 버전 문자열 (예: "24.3.119.0").</summary>
    public string Version { get; }

    public int ProcessId { get; }

    public string PluginVersion { get; }

    public int ProtocolVersion { get; }
}
