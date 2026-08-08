using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Core.Cad;

/// <summary>
/// 연결 상태 전이를 순수 함수로 분리한다 - I/O 없이 단위 테스트하기 위해서다 (Milestone 1 §41).
/// Desktop의 ConnectionManager가 실제 Discovery/Pipe 결과를 <see cref="ConnectionSignal"/>로 바꿔 이 함수에 넘긴다.
/// </summary>
public static class CadConnectionStateEvaluator
{
    public static CadConnectionState Evaluate(CadConnectionState current, ConnectionSignal signal)
    {
        switch (signal)
        {
            case ConnectionSignal.NoAutoCadProcessFound:
                return CadConnectionState.NoAutoCadProcess;
            case ConnectionSignal.ProcessFoundPluginUnreachable:
                return CadConnectionState.PluginUnavailable;
            case ConnectionSignal.MultipleInstancesAwaitingSelection:
                return CadConnectionState.ProcessDetected;
            case ConnectionSignal.ConnectAttemptStarted:
                return CadConnectionState.Connecting;
            case ConnectionSignal.ConnectSucceeded:
            case ConnectionSignal.HeartbeatSucceeded:
                return CadConnectionState.Connected;
            case ConnectionSignal.HeartbeatFailed:
                // 처음 한 번은 재연결을 시도하고, Reconnecting 상태에서 또 실패하면 그때 Disconnected로 확정한다.
                // 순간적인 hiccup 하나로 바로 Disconnected로 떨어져 UI가 깜빡이는 것을 막기 위함.
                return current == CadConnectionState.Connected
                    ? CadConnectionState.Reconnecting
                    : CadConnectionState.Disconnected;
            case ConnectionSignal.ManualDisconnect:
                return CadConnectionState.Disconnected;
            case ConnectionSignal.Faulted:
                return CadConnectionState.Faulted;
            default:
                return current;
        }
    }
}
