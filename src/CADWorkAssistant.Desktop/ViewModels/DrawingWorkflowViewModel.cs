using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// "전체 보기 → 영역 선택 → 선택 확인 → (Zoom/Isolate/Layer만 보기) → 필요하면 Export" Workflow
/// 전체를 담당한다 (Milestone 5 §2). Navigation(Zoom)과 Selection 책임을 하나로 합쳤다 - 실제로는
/// 같은 SelectionSession을 두고 서로 다른 동작을 트리거하는 것뿐이라 나누면 오히려 상태를 두 곳에
/// 나눠 갖는 꼴이 된다(§80 "Selection VM과 책임이 너무 겹치면 하나로 합쳐도 된다"). Layer/Export는
/// 별도 책임이 뚜렷해 각자의 ViewModel로 분리하고 여기서 구성(composition)한다(§81-82).
/// </summary>
public sealed class DrawingWorkflowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly RelayCommand _refreshOverviewCommand;
    private readonly RelayCommand _zoomExtentsCommand;
    private readonly RelayCommand _runSelectionCommand;
    private readonly RelayCommand _zoomToSelectionCommand;
    private readonly RelayCommand _isolateSelectionCommand;
    private readonly RelayCommand _isolateSelectedLayersCommand;
    private readonly RelayCommand _restoreCommand;

    private bool _isBusy;
    private string _statusText = "도면 개요를 불러오는 중...";
    private bool _isError;
    private SelectionMode _selectionMode = SelectionMode.Crossing;
    private SelectionSession? _session;
    private bool _isIsolationActive;
    private int? _overviewObjectCount;
    private int? _overviewLayerCount;

    public DrawingWorkflowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        Rows = new ObservableCollection<DrawingObjectRow>();
        Layers = new LayerWorkflowViewModel(connectionManager);
        Export = new ExportWorkflowViewModel(connectionManager);

        _refreshOverviewCommand = new RelayCommand(RefreshOverview, CanRunCommand);
        _zoomExtentsCommand = new RelayCommand(ZoomExtents, CanRunCommand);
        _runSelectionCommand = new RelayCommand(RunSelection, CanRunCommand);
        _zoomToSelectionCommand = new RelayCommand(ZoomToSelection, () => CanRunCommand() && _session?.Bounds is not null);
        _isolateSelectionCommand = new RelayCommand(IsolateSelection, () => CanRunCommand() && _session is { ObjectCount: > 0 });
        _isolateSelectedLayersCommand = new RelayCommand(IsolateSelectedLayers, () => CanRunCommand() && _session is { ObjectCount: > 0 });
        _restoreCommand = new RelayCommand(Restore, () => CanRunCommand() && _isIsolationActive);

        _connectionManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAutoCadConnectionManager.State))
            {
                RaiseAllCanExecuteChanged();
            }
        };
    }

    public ObservableCollection<DrawingObjectRow> Rows { get; }

    public LayerWorkflowViewModel Layers { get; }

    public ExportWorkflowViewModel Export { get; }

    public ICommand RefreshOverviewCommand => _refreshOverviewCommand;
    public ICommand ZoomExtentsCommand => _zoomExtentsCommand;
    public ICommand RunSelectionCommand => _runSelectionCommand;
    public ICommand ZoomToSelectionCommand => _zoomToSelectionCommand;
    public ICommand IsolateSelectionCommand => _isolateSelectionCommand;
    public ICommand IsolateSelectedLayersCommand => _isolateSelectedLayersCommand;
    public ICommand RestoreCommand => _restoreCommand;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseAllCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public Brush StatusBrush => (Brush?)Application.Current?.TryFindResource(StatusBrushKey) ?? Brushes.Gray;

    private string StatusBrushKey => IsBusy ? "BrushWarning" : IsError ? "BrushError" : "BrushSuccess";

    public SelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (SetProperty(ref _selectionMode, value))
            {
                OnPropertyChanged(nameof(IsWindowMode));
                OnPropertyChanged(nameof(IsCrossingMode));
            }
        }
    }

    // §26-28: Window/Crossing 라디오 - FaceMode(Milestone 4.5에서 실제로 겪은 PropertyChanged 누락
    // 버그)와 같은 함정을 피하려고 SelectionMode setter에서 두 파생 프로퍼티를 직접 raise한다.
    public bool IsWindowMode
    {
        get => SelectionMode == SelectionMode.Window;
        set { if (value) SelectionMode = SelectionMode.Window; }
    }

    public bool IsCrossingMode
    {
        get => SelectionMode == SelectionMode.Crossing;
        set { if (value) SelectionMode = SelectionMode.Crossing; }
    }

    public bool HasSelection => _session is { ObjectCount: > 0 };

    public string SelectionSummaryText => _session switch
    {
        null => "아직 선택한 객체가 없습니다.",
        { ObjectCount: 0 } => "선택 영역에 객체가 없습니다.",
        { } s => $"{s.ObjectCount}개 객체 선택됨 · {s.LayerCounts.Count}개 Layer"
    };

    public string? TypeSummaryText => _session is { ObjectCount: > 0 } s
        ? string.Join(", ", s.TypeCounts.Select(t => $"{t.ObjectType} {t.Count}"))
        : null;

    public bool IsIsolationActive
    {
        get => _isIsolationActive;
        private set
        {
            if (SetProperty(ref _isIsolationActive, value))
            {
                _restoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string OverviewText => _overviewObjectCount is { } count
        ? $"객체 {count}개 · Layer {_overviewLayerCount}개"
        : "—";

    private bool CanRunCommand() => !IsBusy && _connectionManager.State == CadConnectionState.Connected;

    private async void RefreshOverview()
    {
        IsBusy = true;
        IsError = false;
        StatusText = "도면 개요를 불러오는 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.GetDrawingOverview, payload: null, System.Threading.CancellationToken.None)
                .ConfigureAwait(true);

            if (!response.Success)
            {
                ApplyFailure(response.Error!, "도면 개요를 불러오지 못했습니다.");
                return;
            }

            var overview = response.DeserializePayload<DrawingOverviewResponse>();
            _overviewObjectCount = overview?.ObjectCount;
            _overviewLayerCount = overview?.LayerCount;
            OnPropertyChanged(nameof(OverviewText));

            StatusText = overview is { ObjectCount: 0 }
                ? "도면에 객체가 없습니다."
                : $"도면 개요를 불러왔습니다 ({overview!.ObjectCount}개 객체).";
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "GetDrawingOverview failed");
            ApplyError("도면 개요를 불러오지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private async void ZoomExtents()
    {
        IsBusy = true;
        IsError = false;
        StatusText = "전체 도면을 화면에 맞추는 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.ZoomExtents, payload: null, System.Threading.CancellationToken.None)
                .ConfigureAwait(true);

            StatusText = response.Success
                ? "전체 도면을 화면에 맞췄습니다."
                : DescribeError(response.Error!, "전체 보기에 실패했습니다.");
            IsError = !response.Success;
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "ZoomExtents failed");
            ApplyError("전체 보기에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private async void RunSelection()
    {
        IsBusy = true;
        IsError = false;
        StatusText = "AutoCAD에서 영역 선택 대기 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.SelectDrawingObjects, new SelectDrawingObjectsRequest(SelectionMode), System.Threading.CancellationToken.None)
                .ConfigureAwait(true);

            if (!response.Success)
            {
                if (response.Error!.Code == IpcErrorCode.SelectionCancelled)
                {
                    StatusText = "선택이 취소되었습니다.";
                    IsError = false;
                }
                else
                {
                    ApplyFailure(response.Error!, "영역 선택에 실패했습니다.");
                }

                return;
            }

            var selection = response.DeserializePayload<DrawingSelectionResponse>();
            if (selection is null)
            {
                ApplyError("영역 선택에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
                return;
            }

            _session = SelectionSession.Create(_connectionManager.Drawing?.DocumentDisplayName, selection.Objects, System.DateTimeOffset.Now);
            RebuildRows(_session);
            NotifySelectionChanged();

            StatusText = _session.ObjectCount == 0
                ? "선택 영역에 객체가 없습니다."
                : $"{_session.ObjectCount}개 객체를 선택했습니다.";
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "SelectDrawingObjects failed");
            ApplyError("영역 선택에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private async void ZoomToSelection()
    {
        if (_session?.Bounds is not { } bounds)
        {
            return;
        }

        IsBusy = true;
        StatusText = "선택 영역을 화면에 맞추는 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.ZoomToBounds, new ZoomToBoundsRequest(bounds), System.Threading.CancellationToken.None)
                .ConfigureAwait(true);

            StatusText = response.Success
                ? "선택 영역을 화면에 맞췄습니다."
                : DescribeError(response.Error!, "선택 영역 보기에 실패했습니다.");
            IsError = !response.Success;
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "ZoomToBounds failed");
            ApplyError("선택 영역 보기에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private async void IsolateSelection()
    {
        if (_session is not { ObjectCount: > 0 } session)
        {
            return;
        }

        IsBusy = true;
        StatusText = "선택 객체만 표시하는 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.IsolateObjects, new IsolateObjectsRequest(session.Handles), System.Threading.CancellationToken.None)
                .ConfigureAwait(true);

            if (response.Success)
            {
                IsIsolationActive = true;
                StatusText = $"선택한 {session.ObjectCount}개 객체만 표시 중입니다.";
            }
            else
            {
                ApplyFailure(response.Error!, "객체 격리에 실패했습니다.");
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "IsolateObjects failed");
            ApplyError("객체 격리에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private async void IsolateSelectedLayers()
    {
        if (_session is not { ObjectCount: > 0 } session)
        {
            return;
        }

        var layerNames = session.LayerCounts.Select(l => l.LayerName).ToList();

        IsBusy = true;
        StatusText = "선택 Layer만 표시하는 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var succeeded = await Layers.IsolateLayersAsync(layerNames).ConfigureAwait(true);
            if (succeeded)
            {
                IsIsolationActive = true;
                StatusText = $"{layerNames.Count}개 Layer만 표시 중입니다.";
            }
            else
            {
                ApplyError("Layer 격리에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
            }
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private async void Restore()
    {
        IsBusy = true;
        StatusText = "원래 상태로 복원하는 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.RestoreVisibility, payload: null, System.Threading.CancellationToken.None)
                .ConfigureAwait(true);

            if (response.Success)
            {
                IsIsolationActive = false;
                StatusText = "원래 상태로 복원했습니다.";
                IsError = false;
                await Layers.RefreshAsync().ConfigureAwait(true);
            }
            else
            {
                ApplyFailure(response.Error!, "복원에 실패했습니다.");
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "RestoreVisibility failed");
            ApplyError("복원에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    /// <summary>산출 도구들과 달리 Drawing 탭은 활성화될 때마다 최신 개요/Layer 목록을 자동으로
    /// 불러온다 - MainWindowViewModel이 Drawing 탭 선택 시 호출한다. Simulation Mode에서 실제로
    /// 렌더링해보고 나서야 발견한 버그: RefreshOverview()는 첫 await 전에 동기적으로 IsBusy=true를
    /// 설정하는데, async void의 동기 구간이 끝나야 호출자(OnActivated)로 제어가 돌아온다 - 그래서
    /// 바로 다음 줄의 CanRunCommand()(!IsBusy 포함) 검사가 이미 false가 되어 Layer 목록을 부르는
    /// 조건 자체가 항상 스킵됐다. Layer 로드는 Overview와 완전히 독립적인 요청이라 IsBusy에 얽맬
    /// 이유가 없다 - 연결 상태만 직접 확인한다.</summary>
    public void OnActivated()
    {
        var isConnected = _connectionManager.State == CadConnectionState.Connected;

        if (_overviewObjectCount is null && isConnected)
        {
            RefreshOverview();
        }

        if (!Layers.HasLayers && isConnected)
        {
            _ = Layers.RefreshAsync();
        }
    }

    private void RebuildRows(SelectionSession session)
    {
        Rows.Clear();
        foreach (var obj in session.Objects)
        {
            Rows.Add(new DrawingObjectRow(obj.Handle, obj.ObjectType, obj.LayerName));
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionSummaryText));
        OnPropertyChanged(nameof(TypeSummaryText));
        Export.SetSession(_session);
    }

    private void ApplyFailure(IpcError error, string genericMessage)
    {
        StatusText = DescribeError(error, genericMessage);
        IsError = true;
    }

    private void ApplyError(string message)
    {
        StatusText = message;
        IsError = true;
    }

    private static string DescribeError(IpcError error, string genericMessage) => error.Code switch
    {
        IpcErrorCode.NoActiveDocument => "열려 있는 도면이 없습니다.",
        IpcErrorCode.Timeout => "AutoCAD 응답이 지연되고 있습니다.\n\n잠시 후 다시 시도해주세요.",
        _ => genericMessage + "\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요."
    };

    private void RaiseAllCanExecuteChanged()
    {
        _refreshOverviewCommand.RaiseCanExecuteChanged();
        _zoomExtentsCommand.RaiseCanExecuteChanged();
        _runSelectionCommand.RaiseCanExecuteChanged();
        _zoomToSelectionCommand.RaiseCanExecuteChanged();
        _isolateSelectionCommand.RaiseCanExecuteChanged();
        _isolateSelectedLayersCommand.RaiseCanExecuteChanged();
        _restoreCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(StatusBrush));
    }
}
