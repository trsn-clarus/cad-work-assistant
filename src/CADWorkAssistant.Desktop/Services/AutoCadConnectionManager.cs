using System.ComponentModel;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Infrastructure.Ipc;
using Serilog;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>
/// Discover → Connect → Heartbeat → Reconnect 전체 lifecycle을 하나의 백그라운드 루프에서 관리한다.
/// 상태 전이 자체는 <see cref="CadConnectionStateEvaluator"/>(순수 함수)에 위임하고, 여기서는
/// 실제 I/O(Discovery, Named Pipe)와 UI 스레드 marshaling만 담당한다 (§22, §37).
/// </summary>
public sealed class AutoCadConnectionManager : IAutoCadConnectionManager
{
    private readonly IAutoCadDiscoveryService _discovery;
    private readonly SynchronizationContext? _uiContext;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private AutoCadPipeClient? _client;

    private CadConnectionState _state = CadConnectionState.NoAutoCadProcess;
    private AutoCadInstanceInfo? _instance;
    private DrawingContext? _drawing;
    private IReadOnlyList<AutoCadInstanceCandidate> _availableInstances = Array.Empty<AutoCadInstanceCandidate>();
    private int? _selectedProcessId;

    public AutoCadConnectionManager(IAutoCadDiscoveryService discovery)
    {
        _discovery = discovery;
        // WPF는 UI 스레드에 DispatcherSynchronizationContext를 심어둔다. 이 값은 반드시
        // UI 스레드에서 생성자가 호출될 때 캡처해야 한다 (App.xaml.cs에서 구성).
        _uiContext = SynchronizationContext.Current;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CadConnectionState State => _state;

    public AutoCadInstanceInfo? Instance => _instance;

    public DrawingContext? Drawing => _drawing;

    public IReadOnlyList<AutoCadInstanceCandidate> AvailableInstances => _availableInstances;

    public int? SelectedProcessId => _selectedProcessId;

    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task<IpcResponseEnvelope> SendRequestAsync(string messageType, object? payload, CancellationToken cancellationToken)
    {
        if (_client is not { IsConnected: true } client)
        {
            throw new InvalidOperationException("Not connected to AutoCAD - SendRequestAsync called while disconnected.");
        }

        return await client
            .SendRequestAsync(messageType, payload, IpcProtocol.RequestTimeoutMs, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SelectInstanceAsync(int processId, CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DisposeClient();
            SetSelectedProcessId(processId);
            await TryConnectAsync(processId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await TickAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _operationLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AutoCAD connection monitor tick failed unexpectedly");
            }

            try
            {
                await Task.Delay(IpcProtocol.HeartbeatIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            if (await TryHeartbeatAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            DisposeClient();
            ApplySignal(ConnectionSignal.HeartbeatFailed); // Connected -> Reconnecting (첫 실패)

            if (_state == CadConnectionState.Reconnecting && _selectedProcessId is int pid)
            {
                // 순간적인 hiccup이었을 수 있으니 다음 heartbeat 간격을 기다리지 않고 바로 한 번 더 시도한다.
                if (await TryConnectAsync(pid, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                ApplySignal(ConnectionSignal.HeartbeatFailed); // Reconnecting -> Disconnected (재시도도 실패)
            }

            return;
        }

        await DiscoverAndConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DiscoverAndConnectAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<AutoCadInstanceCandidate> candidates;
        try
        {
            candidates = await _discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Warning(ex, "AutoCAD discovery failed");
            ApplySignal(ConnectionSignal.Faulted);
            return;
        }

        SetAvailableInstances(candidates);

        if (candidates.Count == 0)
        {
            SetSelectedProcessId(null);
            ApplySignal(ConnectionSignal.NoAutoCadProcessFound);
            return;
        }

        var reachable = candidates.Where(c => c.PluginReachable).ToList();
        if (reachable.Count == 0)
        {
            ApplySignal(ConnectionSignal.ProcessFoundPluginUnreachable);
            return;
        }

        if (_selectedProcessId is int selected && reachable.Any(c => c.ProcessId == selected))
        {
            await TryConnectAsync(selected, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (reachable.Count == 1)
        {
            await TryConnectAsync(reachable[0].ProcessId, cancellationToken).ConfigureAwait(false);
            return;
        }

        // 여러 Instance가 있고 아직 선택되지 않았다 - SelectInstanceAsync 호출을 기다린다 (§23, §36).
        ApplySignal(ConnectionSignal.MultipleInstancesAwaitingSelection);
    }

    private async Task<bool> TryConnectAsync(int processId, CancellationToken cancellationToken)
    {
        ApplySignal(ConnectionSignal.ConnectAttemptStarted);

        var client = new AutoCadPipeClient();
        try
        {
            await client.ConnectAsync(processId, IpcProtocol.ConnectTimeoutMs, cancellationToken).ConfigureAwait(false);
            var infoResponse = await client
                .SendRequestAsync(IpcMessageTypes.GetApplicationInfo, payload: null, IpcProtocol.RequestTimeoutMs, cancellationToken)
                .ConfigureAwait(false);

            if (!infoResponse.Success)
            {
                client.Dispose();
                ApplySignal(ConnectionSignal.ProcessFoundPluginUnreachable);
                return false;
            }

            _client = client;
            SetSelectedProcessId(processId);
            SetInstance(infoResponse.DeserializePayload<AutoCadInstanceInfo>());
            ApplySignal(ConnectionSignal.ConnectSucceeded);

            await RefreshDrawingContextAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Debug(ex, "Failed to connect to AutoCAD process {ProcessId}", processId);
            client.Dispose();
            ApplySignal(ConnectionSignal.ProcessFoundPluginUnreachable);
            return false;
        }
    }

    private async Task<bool> TryHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (_client is not { } client)
        {
            return false;
        }

        try
        {
            var response = await client
                .SendRequestAsync(IpcMessageTypes.Ping, payload: null, IpcProtocol.RequestTimeoutMs, cancellationToken)
                .ConfigureAwait(false);

            if (!response.Success)
            {
                return false;
            }

            await RefreshDrawingContextAsync(cancellationToken).ConfigureAwait(false);
            ApplySignal(ConnectionSignal.HeartbeatSucceeded);
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Debug(ex, "AutoCAD heartbeat failed");
            return false;
        }
    }

    private async Task RefreshDrawingContextAsync(CancellationToken cancellationToken)
    {
        if (_client is not { } client)
        {
            return;
        }

        try
        {
            var response = await client
                .SendRequestAsync(IpcMessageTypes.GetDrawingContext, payload: null, IpcProtocol.RequestTimeoutMs, cancellationToken)
                .ConfigureAwait(false);

            // 문서가 없는 것(NoActiveDocument)은 정상적인 상태다 - heartbeat 실패로 취급하지 않는다.
            SetDrawing(response.Success ? response.DeserializePayload<DrawingContext>() : null);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Debug(ex, "Failed to refresh AutoCAD drawing context");
        }
    }

    private void ApplySignal(ConnectionSignal signal) => SetState(CadConnectionStateEvaluator.Evaluate(_state, signal));

    private void DisposeClient()
    {
        _client?.Dispose();
        _client = null;
        SetInstance(null);
        SetDrawing(null);
    }

    private void SetState(CadConnectionState value)
    {
        if (_state == value)
        {
            return;
        }

        _state = value;
        RaisePropertyChanged(nameof(State));
    }

    private void SetInstance(AutoCadInstanceInfo? value)
    {
        _instance = value;
        RaisePropertyChanged(nameof(Instance));
    }

    private void SetDrawing(DrawingContext? value)
    {
        _drawing = value;
        RaisePropertyChanged(nameof(Drawing));
    }

    private void SetAvailableInstances(IReadOnlyList<AutoCadInstanceCandidate> value)
    {
        _availableInstances = value;
        RaisePropertyChanged(nameof(AvailableInstances));
    }

    private void SetSelectedProcessId(int? value)
    {
        if (_selectedProcessId == value)
        {
            return;
        }

        _selectedProcessId = value;
        RaisePropertyChanged(nameof(SelectedProcessId));
    }

    private void RaisePropertyChanged(string propertyName)
    {
        void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (_uiContext is null)
        {
            Raise();
        }
        else
        {
            _uiContext.Post(_ => Raise(), null);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // 종료 중 취소로 인한 예외는 무시한다.
        }

        DisposeClient();
        _cts?.Dispose();
        _operationLock.Dispose();
    }
}
