using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// "AutoCAD 영역 선택 → 닫힘 확인 → 면적 계산 → 단위 변환 → 합산 → 결과 표시 → 산출내역 저장" Workflow
/// 전체를 담당한다. Length의 LengthWorkflowViewModel과 같은 구조를 그대로 따른다 (Milestone 3 §5, §46) -
/// AutoCAD API도, Named Pipe도 직접 모른다. 계산은 전부 CADWorkAssistant.Core.Area에 위임한다.
/// </summary>
public sealed class AreaWorkflowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly RelayCommand _runSelectionCommand;
    private readonly RelayCommand _addToQuantitySheetCommand;

    private AreaWorkflowState _state = AreaWorkflowState.Idle;
    private string _statusText = "Ready";
    private AreaMeasurementResult? _result;

    public AreaWorkflowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        Rows = new ObservableCollection<AreaObjectRow>();

        _runSelectionCommand = new RelayCommand(RunSelection, CanRunSelection);
        _addToQuantitySheetCommand = new RelayCommand(AddToQuantitySheet, CanAddToQuantitySheet);
        CopyResultCommand = new RelayCommand(CopyResult, () => _result?.DisplayValueSquareMeters is not null);

        _connectionManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAutoCadConnectionManager.State))
            {
                _runSelectionCommand.RaiseCanExecuteChanged();
            }
        };
    }

    public ObservableCollection<AreaObjectRow> Rows { get; }

    public ICommand RunSelectionCommand => _runSelectionCommand;

    public ICommand AddToQuantitySheetCommand => _addToQuantitySheetCommand;

    public ICommand CopyResultCommand { get; }

    /// <summary>MainWindowViewModel이 구독해서 자신의 QuantityRecords에 붙인다 - 소유권을 갖지 않는다.</summary>
    public event System.EventHandler<QuantityRecord>? RecordAdded;

    public AreaWorkflowState State
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

    public bool IsBusy => _state == AreaWorkflowState.AwaitingSelection;

    /// <summary>Cancel/EmptySelection/NoValidObjects는 오류가 아니다 - UI가 빨간 상태를 주는 건 진짜 Error일 때뿐이다.</summary>
    public bool IsErrorState => _state == AreaWorkflowState.Error;

    public string? TotalDisplay => _result?.DisplayValueSquareMeters is { } squareMeters
        ? AreaFormatter.FormatSquareMetersWithUnit(squareMeters)
        : null;

    /// <summary>열림/미지원/비정상형상으로 제외된 항목을 하나의 문장으로 요약한다 - 배너를 여러 개 쌓지 않는다 (§65).</summary>
    public string? ExcludedSummary
    {
        get
        {
            if (_result is not { ExcludedCount: > 0 } result)
            {
                return null;
            }

            var parts = new System.Collections.Generic.List<string>();
            if (result.OpenItems.Count > 0)
            {
                parts.Add($"열린 형상 {result.OpenItems.Count}개");
            }

            if (result.UnsupportedItems.Count > 0)
            {
                var typeNames = string.Join(", ", result.UnsupportedItems.Select(i => i.ObjectType).Distinct());
                parts.Add($"미지원 객체 {result.UnsupportedItems.Count}개({typeNames})");
            }

            if (result.InvalidGeometryItems.Count > 0)
            {
                parts.Add($"비정상 형상 {result.InvalidGeometryItems.Count}개");
            }

            return $"선택한 {result.SelectedCount}개 객체 중 {result.ExcludedCount}개는 면적 계산에서 제외했습니다 ({string.Join(", ", parts)}).";
        }
    }

    public bool HasExcludedSummary => ExcludedSummary is not null;

    /// <summary>색만으로 상태를 전달하지 않는다 - StatusText가 항상 같이 보인다 (UX 가이드).</summary>
    public Brush StatusBrush => (Brush?)Application.Current?.TryFindResource(StatusBrushKey) ?? Brushes.Gray;

    private string StatusBrushKey => _state switch
    {
        AreaWorkflowState.Success => "BrushSuccess",
        AreaWorkflowState.PartialSuccess => "BrushWarning",
        AreaWorkflowState.Error => "BrushError",
        AreaWorkflowState.AwaitingSelection => "BrushWarning",
        _ => "BrushTextMuted"
    };

    private bool CanRunSelection() => !IsBusy && _connectionManager.State == CadConnectionState.Connected;

    // §19: 총 면적이 0에 가까우면 산출내역으로 저장하지 않는다 - 저장할 의미 있는 값이 없다.
    private bool CanAddToQuantitySheet() =>
        (_state == AreaWorkflowState.Success || _state == AreaWorkflowState.PartialSuccess)
        && _result?.DisplayValueSquareMeters is { } total
        && total > AreaAggregationService.AreaEpsilon;

    private async void RunSelection()
    {
        if (IsBusy)
        {
            return; // 중복 요청 방지.
        }

        State = AreaWorkflowState.AwaitingSelection;
        StatusText = "AutoCAD에서 영역 선택 대기 중...";

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.SelectAreaObjects, payload: null, System.Threading.CancellationToken.None)
                .ConfigureAwait(true); // WPF 컨트롤을 계속 건드려야 하므로 UI 스레드로 돌아온다.

            if (!response.Success)
            {
                ApplyFailure(response.Error!);
                return;
            }

            var selection = response.DeserializePayload<AreaSelectionResponse>();
            if (selection is null)
            {
                ApplyError("면적을 계산하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
                return;
            }

            if (selection.Objects.Count == 0 && selection.ExcludedObjectTypeNames.Count == 0)
            {
                _result = null;
                Rows.Clear();
                State = AreaWorkflowState.EmptySelection;
                StatusText = "선택된 객체가 없습니다.";
                NotifyResultChanged();
                return;
            }

            var drawingName = _connectionManager.Drawing?.DocumentDisplayName;
            var result = AreaAggregationService.Aggregate(selection, drawingName, System.DateTimeOffset.Now);

            _result = result;
            RebuildRows(result);
            NotifyResultChanged();

            ApplyResult(result);
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "Area selection failed");
            ApplyError("면적을 계산하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
    }

    private void ApplyResult(AreaMeasurementResult result)
    {
        if (result.SupportedCount == 0)
        {
            State = AreaWorkflowState.NoValidObjects;
            StatusText = $"선택한 {result.SelectedCount}개 객체 모두 면적 계산에 사용할 수 없습니다.";
            return;
        }

        if (result.DisplayValueSquareMeters is null)
        {
            // Unitless라 변환은 못 했지만 형상 자체는 유효하다 - Error가 아니다.
            State = result.SupportedCount < result.SelectedCount ? AreaWorkflowState.PartialSuccess : AreaWorkflowState.Success;
            StatusText = "도면 단위가 설정되어 있지 않습니다.";
            return;
        }

        if (result.SupportedCount < result.SelectedCount)
        {
            State = AreaWorkflowState.PartialSuccess;
            StatusText = $"{result.SelectedCount}개 중 {result.SupportedCount}개 영역의 면적을 계산했습니다.";
            return;
        }

        State = AreaWorkflowState.Success;
        StatusText = $"{result.SupportedCount}개 영역의 면적을 계산했습니다.";
    }

    private void ApplyFailure(IpcError error)
    {
        switch (error.Code)
        {
            case IpcErrorCode.SelectionCancelled:
                State = AreaWorkflowState.Cancelled;
                StatusText = "선택이 취소되었습니다.";
                break;
            case IpcErrorCode.NoActiveDocument:
                ApplyError("열려 있는 도면이 없습니다.");
                break;
            case IpcErrorCode.Timeout:
                ApplyError("AutoCAD 응답이 지연되고 있습니다.\n\n잠시 후 다시 시도해주세요.");
                break;
            default:
                ApplyError("면적을 계산하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
                break;
        }
    }

    private void ApplyError(string message)
    {
        State = AreaWorkflowState.Error;
        StatusText = message;
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(ExcludedSummary));
        OnPropertyChanged(nameof(HasExcludedSummary));
    }

    private void RebuildRows(AreaMeasurementResult result)
    {
        Rows.Clear();
        foreach (var item in result.Items.Where(i => i.Status == AreaObjectStatus.Valid))
        {
            var display = AreaUnitConverter.TryConvertToSquareMeters(item.RawArea, result.SourceUnit, out var squareMeters)
                ? AreaFormatter.FormatSquareMetersWithUnit(squareMeters)
                : $"{item.RawArea:N2} ({DrawingUnitDisplay.SquaredAbbreviation(result.SourceUnit)})";

            Rows.Add(new AreaObjectRow(item.Handle, item.ObjectType, item.LayerName, display));
        }
    }

    private void AddToQuantitySheet()
    {
        if (_result is not { DisplayValueSquareMeters: { } total } result)
        {
            return;
        }

        var validItems = result.Items.Where(i => i.Status == AreaObjectStatus.Valid).ToList();

        var layer = validItems.Select(i => i.LayerName).Distinct().Count() == 1
            ? validItems[0].LayerName
            : "Mixed";

        // §70: 변환된(㎡) 개별 값을 더해 변환된 총계와 눈으로 맞아떨어지는 산식을 남긴다.
        var expression =
            string.Join(" + ", validItems.Select(i =>
                AreaUnitConverter.TryConvertToSquareMeters(i.RawArea, result.SourceUnit, out var m) ? AreaFormatter.FormatSquareMeters(m) : "?"))
            + $" = {AreaFormatter.FormatSquareMetersWithUnit(total)}";

        var record = new QuantityRecord(
            id: "Q-" + System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            type: "Area",
            layer: layer,
            objectCount: result.SupportedCount,
            value: (decimal)total,
            unit: "m²",
            sourceDrawing: result.DrawingName ?? "Unknown",
            createdAt: result.CreatedAt,
            rawValue: (decimal)result.RawTotalArea,
            sourceUnit: DrawingUnitDisplay.SquaredAbbreviation(result.SourceUnit),
            objectHandles: validItems.Select(i => i.Handle).ToList(),
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
