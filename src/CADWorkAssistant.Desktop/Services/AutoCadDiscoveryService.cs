using System;
using System.Diagnostics;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>
/// ViewModel은 이 서비스를 통해서만 AutoCAD 프로세스를 찾는다 - Process.GetProcessesByName을
/// 직접 호출하지 않는다 (§21). 각 프로세스마다 아주 짧은 timeout으로 Ping을 보내 Plugin 로드 여부를 확인한다.
///
/// 개발용 Simulation Mode: 환경변수 CWA_USE_FAKE_AUTOCAD=1이 설정되어 있으면 "acad" 대신
/// CADWorkAssistant.FakeAutoCad 프로세스를 찾는다. 그 다음부터는 실제 AutoCAD와 완전히 동일한
/// IPC 경로(Discovery → ConnectionManager → Pipe)를 그대로 탄다 - Desktop 코드에 Fake 분기가 없다
/// (Milestone 2 §10, §73 - Production 코드/설치본에는 이 프로세스 이름만 있을 뿐 FakeAutoCAD 자체는 포함되지 않는다).
/// </summary>
public sealed class AutoCadDiscoveryService : IAutoCadDiscoveryService
{
    private const string RealAutoCadProcessName = "acad";
    private const string FakeAutoCadProcessName = "CADWorkAssistant.FakeAutoCad";
    private const string SimulationModeEnvironmentVariable = "CWA_USE_FAKE_AUTOCAD";

    // 여러 AutoCAD 인스턴스를 순서대로 프로브하는 동안 UI가 멈춘 것처럼 보이지 않도록 짧게 유지한다.
    private const int ProbeTimeoutMs = 300;

    public async Task<IReadOnlyList<AutoCadInstanceCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var processName = Environment.GetEnvironmentVariable(SimulationModeEnvironmentVariable) == "1"
            ? FakeAutoCadProcessName
            : RealAutoCadProcessName;

        var processes = Process.GetProcessesByName(processName);
        var candidates = new List<AutoCadInstanceCandidate>(processes.Length);

        foreach (var process in processes)
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidates.Add(await ProbeAsync(process.Id, cancellationToken).ConfigureAwait(false));
            }
        }

        return candidates;
    }

    private static async Task<AutoCadInstanceCandidate> ProbeAsync(int processId, CancellationToken cancellationToken)
    {
        using var client = new AutoCadPipeClient();

        try
        {
            await client.ConnectAsync(processId, ProbeTimeoutMs, cancellationToken).ConfigureAwait(false);
            var response = await client
                .SendRequestAsync(IpcMessageTypes.Ping, payload: null, ProbeTimeoutMs, cancellationToken)
                .ConfigureAwait(false);

            return new AutoCadInstanceCandidate(processId, pluginReachable: response.Success, activeDrawingName: null);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested is false)
        {
            // Pipe가 없거나(Plugin 미로드), Timeout이거나 - 어느 쪽이든 "도달 불가"로 취급한다.
            return new AutoCadInstanceCandidate(processId, pluginReachable: false, activeDrawingName: null);
        }
    }
}
