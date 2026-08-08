using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// "AutoCAD 객체 선택 → 길이 추출 → 단위 변환 → 합산 → 결과 표시 → 산출내역 저장" Workflow 전체를
/// 담당한다 (Milestone 2 §0). AutoCAD API도, Named Pipe도 직접 모른다 - IAutoCadConnectionManager를
/// 통해서만 이야기하고, 계산은 전부 CADWorkAssistant.Core.Length에 위임한다.
/// </summary>
public sealed class LengthWorkflowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly RelayCommand _runSelectionCommand;
    private readonly RelayCommand _addToQuantitySheetCommand;

    private LengthWorkflowState _state = LengthWorkflowState.Idle;
    private string _statusText = "Ready";
    private LengthMeasurementResult? _result;
    private LengthMeasurementResult? _lastSuccessfulResult;

    public LengthWorkflowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        Rows = new ObservableCollection<LengthObjectRow>();

        _runSelectionCommand = new RelayCommand(RunSelection, CanRunSelection);
        _addToQuantitySheetCommand = new RelayCommand(AddToQuantitySheet, CanAddToQuantitySheet);
        CopyResultCommand = new RelayCommand(CopyResult, () => _result?.DisplayValueMeters is not null);

        _connectionManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAutoCadConnectionManager.State))
            {
                _runSelectionCommand.RaiseCanExecuteChanged();
            }
        };
    }

    public ObservableCollection<LengthObjectRow> Rows { get; }

    public ICommand RunSelectionCommand => _runSelectionCommand;

    public ICommand AddToQuantitySheetCommand => _addToQuantitySheetCommand;

    public ICommand CopyResultCommand { get; }

    /// <summary>MainWindowViewModel이 구독해서 자신의 QuantityRecords에 붙인다 - 소유권을 갖지 않는다.</summary>
    public event System.EventHandler<QuantityRecord>? RecordAdded;

    public LengthWorkflowState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsErrorState));
                OnPropertyChanged(nameof(StatusBrush));
                _runSelectionCommand.RaiseCanExecuteChanged();
                _addToQuantitySheetCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy => _state == LengthWorkflowState.AwaitingSelection;

    /// <summary>Cancel/EmptySelection은 오류가 아니다 - UI가 빨간 상태를 주는 건 진짜 Error일 때뿐이다 (§19, §43).</summary>
    public bool IsErrorState => _state == LengthWorkflowState.Error;

    public string? TotalDisplay => _result?.DisplayValueMeters is { } meters ? LengthFormatter.FormatMetersWithUnit(meters) : null;

    /// <summary>단위 없이 값만 - Numeric Typography에서 값과 단위를 다른 hierarchy로 보여줄 때 쓴다 (Milestone 4.5 §16-17).</summary>
    public string? TotalValueDisplay => _result?.DisplayValueMeters is { } meters ? LengthFormatter.FormatMeters(meters) : null;

    /// <summary>가장 최근에 "성공"한 길이 측정 결과 - Vertical Area/Parapet이 "최근 측정값 사용"으로
    /// 재사용한다 (Milestone 4 §17, §50). 화면에 지금 보이는 값(_result, EmptySelection이면 null로
    /// 비워진다)과는 다른 변수다 - 사용자가 이후에 빈 선택/취소를 겪어도 재사용 가능한 값은 남아있어야
    /// 한다.</summary>
    public LengthMeasurementResult? LastResult => _lastSuccessfulResult;

    public string? ExcludedSummary => _result is { ExcludedCount: > 0 } result
        ? $"선택한 객체 중 {result.ExcludedCount}개는 길이 계산을 지원하지 않아 제외했습니다 ({string.Join(", ", result.ExcludedObjectTypeNames)})."
        : null;

    public bool HasExcludedSummary => ExcludedSummary is not null;

    /// <summary>색만으로 상태를 전달하지 않는다 - StatusText가 항상 같이 보인다 (UX 가이드 #37).</summary>
    public Brush StatusBrush => (Brush?)Application.Current?.TryFindResource(StatusBrushKey) ?? Brushes.Gray;

    private string StatusBrushKey => _state switch
    {
        LengthWorkflowState.Success => "BrushSuccess",
        LengthWorkflowState.Error => "BrushError",
        LengthWorkflowState.AwaitingSelection => "BrushWarning",
        _ => "BrushTextMuted"
    };

    private bool CanRunSelection() => !IsBusy && _connectionManager.State == CadConnectionState.Connected;

    private bool CanAddToQuantitySheet() => _state == LengthWorkflowState.Success && _result?.DisplayValueMeters is not null;

    private async void RunSelection()
    {
        if (IsBusy)
        {
            return; // 중복 요청 방지 (§53).
        }

        State = LengthWorkflowState.AwaitingSelection;
        StatusText = "AutoCAD에서 객체 선택 대기 중...";

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.SelectLengthObjects, payload: null, System.Threading.CancellationToken.None)
                .ConfigureAwait(true); // WPF 컨트롤을 계속 건드려야 하므로 UI 스레드로 돌아온다.

            if (!response.Success)
            {
                ApplyFailure(response.Error!);
                return;
            }

            var selection = response.DeserializePayload<LengthSelectionResponse>();
            if (selection is null)
            {
                ApplyError("길이를 계산하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
                return;
            }

            if (selection.Objects.Count == 0 && selection.ExcludedObjectTypeNames.Count == 0)
            {
                _result = null;
                Rows.Clear();
                State = LengthWorkflowState.EmptySelection;
                StatusText = "선택된 객체가 없습니다.";
                NotifyResultChanged();
                return;
            }

            var drawingName = _connectionManager.Drawing?.DocumentDisplayName;
            var result = LengthAggregationService.Aggregate(selection, drawingName, System.DateTimeOffset.Now);

            _result = result;
            _lastSuccessfulResult = result;
            RebuildRows(result);
            NotifyResultChanged();
            OnPropertyChanged(nameof(LastResult));

            State = LengthWorkflowState.Success;
            StatusText = result.DisplayValueMeters is not null
                ? $"{result.ObjectCount}개 객체의 길이를 계산했습니다."
                : "도면 단위가 설정되어 있지 않습니다.";
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "Length selection failed");
            ApplyError("길이를 계산하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
    }

    private void ApplyFailure(IpcError error)
    {
        switch (error.Code)
        {
            case IpcErrorCode.SelectionCancelled:
                State = LengthWorkflowState.Cancelled;
                StatusText = "선택이 취소되었습니다.";
                break;
            case IpcErrorCode.NoActiveDocument:
                ApplyError("열려 있는 도면이 없습니다.");
                break;
            case IpcErrorCode.Timeout:
                ApplyError("AutoCAD 응답이 지연되고 있습니다.\n\n잠시 후 다시 시도해주세요.");
                break;
            default:
                ApplyError("길이를 계산하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
                break;
        }
    }

    private void ApplyError(string message)
    {
        State = LengthWorkflowState.Error;
        StatusText = message;
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(TotalValueDisplay));
        OnPropertyChanged(nameof(ExcludedSummary));
        OnPropertyChanged(nameof(HasExcludedSummary));
    }

    private void RebuildRows(LengthMeasurementResult result)
    {
        Rows.Clear();
        foreach (var obj in result.Objects)
        {
            var display = LengthUnitConverter.TryConvertToMeters(obj.RawLength, result.SourceUnit, out var meters)
                ? LengthFormatter.FormatMetersWithUnit(meters)
                : $"{obj.RawLength:N3} ({DrawingUnitDisplay.Abbreviation(result.SourceUnit)})";

            Rows.Add(new LengthObjectRow(obj.Handle, obj.GeometryType.ToString(), obj.LayerName, display));
        }
    }

    private void AddToQuantitySheet()
    {
        if (_result is not { DisplayValueMeters: { } meters } result)
        {
            return;
        }

        var layer = result.Objects.Select(o => o.LayerName).Distinct().Count() == 1
            ? result.Objects[0].LayerName
            : "Mixed";

        var expression =
            string.Join(" + ", result.Objects.Select(o => o.RawLength.ToString("N3")))
            + $" {DrawingUnitDisplay.Abbreviation(result.SourceUnit)} = {LengthFormatter.FormatMetersWithUnit(meters)}";

        var record = new QuantityRecord(
            id: "Q-" + System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            type: "Length",
            layer: layer,
            objectCount: result.ObjectCount,
            value: (decimal)meters,
            unit: "m",
            sourceDrawing: result.DrawingName ?? "Unknown",
            createdAt: result.CreatedAt,
            rawValue: (decimal)result.RawTotalLength,
            sourceUnit: DrawingUnitDisplay.Abbreviation(result.SourceUnit),
            objectHandles: result.Objects.Select(o => o.Handle).ToList(),
            calculationExpression: expression);

        RecordAdded?.Invoke(this, record);
        StatusText = "산출내역에 추가했습니다.";
    }

    private void CopyResult()
    {
        if (TotalDisplay is not { } text)
        {
            return;
        }

        Clipboard.SetText(text);
        StatusText = "결과를 클립보드에 복사했습니다.";
    }
}
