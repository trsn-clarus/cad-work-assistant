using System.Diagnostics;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>
/// ViewModel은 이 서비스를 통해서만 AutoCAD 프로세스를 찾는다 - Process.GetProcessesByName을
/// 직접 호출하지 않는다 (§21). 각 프로세스마다 아주 짧은 timeout으로 Ping을 보내 Plugin 로드 여부를 확인한다.
/// </summary>
public sealed class AutoCadDiscoveryService : IAutoCadDiscoveryService
{
    // 여러 AutoCAD 인스턴스를 순서대로 프로브하는 동안 UI가 멈춘 것처럼 보이지 않도록 짧게 유지한다.
    private const int ProbeTimeoutMs = 300;

    public async Task<IReadOnlyList<AutoCadInstanceCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var processes = Process.GetProcessesByName("acad");
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
