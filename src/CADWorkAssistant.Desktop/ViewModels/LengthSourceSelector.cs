using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.VerticalArea;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// "기준 길이를 어떻게 확보할지"(CAD에서 새로 선택 / Length 도구의 최근 측정값 재사용 / 수동 입력)는
/// Vertical Area와 Parapet 둘 다 완전히 똑같이 필요하다 (Milestone 4 §16-19) - 새 AutoCAD Selection
/// Handler 없이 기존 SelectLengthObjects만 쓴다는 점도 같다 (§5-6). 두 ViewModel에 각각 복붙하면
/// 명백한 중복이라 여기 하나로 뽑았다. VerticalAreaWorkflowViewModel/ParapetWorkflowViewModel은
/// 이 타입을 필드로 갖고(상속이 아니라 합성 - 둘의 나머지 상태/동작이 서로 다르기 때문), 결과가
/// 바뀔 때마다 <see cref="Resolved"/>를 구독해 자기 계산을 다시 돈다.
/// </summary>
public sealed class LengthSourceSelector : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly LengthWorkflowViewModel _lengthWorkflow;
    private readonly RelayCommand _runSelectionCommand;

    private bool _isBusy;
    private MeasurementSourceType _sourceType = MeasurementSourceType.CadSelection;
    private LengthMeasurementResult? _cadSelectionResult;
    private string _manualLengthText = string.Empty;
    private DrawingUnit _manualLengthUnit = DrawingUnit.Millimeters;

    private double? _lengthMeters;
    private string? _drawingName;
    private IReadOnlyList<string> _objectHandles = Array.Empty<string>();
    private string? _statusText;
    private bool _isCancelled;
    private bool _isError;

    public LengthSourceSelector(IAutoCadConnectionManager connectionManager, LengthWorkflowViewModel lengthWorkflow)
    {
        _connectionManager = connectionManager;
        _lengthWorkflow = lengthWorkflow;

        _runSelectionCommand = new RelayCommand(RunSelection, CanRunSelection);

        _connectionManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAutoCadConnectionManager.State))
            {
                _runSelectionCommand.RaiseCanExecuteChanged();
            }
        };

        // Length 도구에서 새 측정이 성공할 때마다 "최근 측정값 사용" 라디오의 가용 여부/요약과,
        // 지금 그 소스를 고르고 있다면 실제 길이 값까지 갱신해야 한다. 구독 없이는 이 라디오가
        // 생성 시점 상태에 그대로 멈춰 있다 - 실제로 겪은 버그(Simulation Mode 수동 검증 중 발견).
        _lengthWorkflow.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LengthWorkflowViewModel.LastResult))
            {
                OnPropertyChanged(nameof(HasExistingMeasurement));
                OnPropertyChanged(nameof(ExistingMeasurementSummary));
                if (SourceType == MeasurementSourceType.ExistingMeasurement)
                {
                    Recalculate();
                }
            }
        };

        Recalculate();
    }

    public IReadOnlyList<DrawingUnit> ManualLengthUnitOptions => LinearUnitOptions.All;

    public ICommand RunSelectionCommand => _runSelectionCommand;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _runSelectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCancelled => _isCancelled;

    public bool IsError => _isError;

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public MeasurementSourceType SourceType
    {
        get => _sourceType;
        set
        {
            if (SetProperty(ref _sourceType, value))
            {
                OnPropertyChanged(nameof(IsCadSelectionSource));
                OnPropertyChanged(nameof(IsExistingMeasurementSource));
                OnPropertyChanged(nameof(IsManualSource));
                Recalculate();
            }
        }
    }

    // get은 Visibility 바인딩(조건부 하위 컨트롤 표시)에, set은 RadioButton.IsChecked TwoWay 바인딩에 쓴다.
    public bool IsCadSelectionSource
    {
        get => SourceType == MeasurementSourceType.CadSelection;
        set
        {
            if (value)
            {
                SourceType = MeasurementSourceType.CadSelection;
            }
        }
    }

    public bool IsExistingMeasurementSource
    {
        get => SourceType == MeasurementSourceType.ExistingMeasurement;
        set
        {
            if (value)
            {
                SourceType = MeasurementSourceType.ExistingMeasurement;
            }
        }
    }

    public bool IsManualSource
    {
        get => SourceType == MeasurementSourceType.Manual;
        set
        {
            if (value)
            {
                SourceType = MeasurementSourceType.Manual;
            }
        }
    }

    /// <summary>"최근 측정값 사용" 라디오가 지금 고를 수 있는 값이 있는지 - 없으면 UI가 비활성화한다.</summary>
    public bool HasExistingMeasurement => _lengthWorkflow.LastResult is not null;

    public string ExistingMeasurementSummary => _lengthWorkflow.LastResult is { } result
        ? (result.DisplayValueMeters is { } m
            ? $"{LengthFormatter.FormatMetersWithUnit(m)} · {result.DrawingName ?? "Unknown"}"
            : $"단위 없음 · {result.DrawingName ?? "Unknown"}")
        : "최근 측정값이 없습니다.";

    public string ManualLengthText
    {
        get => _manualLengthText;
        set
        {
            if (SetProperty(ref _manualLengthText, value))
            {
                Recalculate();
            }
        }
    }

    public DrawingUnit ManualLengthUnit
    {
        get => _manualLengthUnit;
        set
        {
            if (SetProperty(ref _manualLengthUnit, value))
            {
                Recalculate();
            }
        }
    }

    public bool HasLength => _lengthMeters is not null;

    public double? LengthMeters => _lengthMeters;

    public string? LengthDisplay => _lengthMeters is { } meters ? LengthFormatter.FormatMetersWithUnit(meters) : null;

    public string? DrawingName => _drawingName;

    public IReadOnlyList<string> ObjectHandles => _objectHandles;

    /// <summary>CAD/기존측정 소스의 Layer 요약(§Length AddToQuantitySheet와 같은 규칙) - 수동 입력이면 "—".</summary>
    public string LayerDisplay
    {
        get
        {
            var sourceResult = SourceType == MeasurementSourceType.CadSelection ? _cadSelectionResult : _lengthWorkflow.LastResult;
            var layers = sourceResult?.Objects.Select(o => o.LayerName).Distinct().ToList() ?? new List<string>();
            return layers.Count switch
            {
                0 => "—",
                1 => layers[0],
                _ => "Mixed"
            };
        }
    }

    private bool CanRunSelection() => !IsBusy && _connectionManager.State == CadConnectionState.Connected;

    private async void RunSelection()
    {
        if (IsBusy)
        {
            return; // 중복 요청 방지.
        }

        IsBusy = true;
        _isCancelled = false;
        _isError = false;
        StatusText = "AutoCAD에서 기준선 선택 대기 중...";
        OnPropertyChanged(nameof(IsCancelled));
        OnPropertyChanged(nameof(IsError));

        try
        {
            var outcome = await LengthSelectionCoordinator
                .SelectFromCadAsync(_connectionManager, _connectionManager.Drawing?.DocumentDisplayName, CancellationToken.None)
                .ConfigureAwait(true);

            switch (outcome.Kind)
            {
                case LengthCoordinatorOutcomeKind.Selected:
                    _cadSelectionResult = outcome.Result;
                    SourceType = MeasurementSourceType.CadSelection; // 값이 같아도 setter가 Recalculate를 보장하진 않으므로 아래서 명시적으로 다시 부른다.
                    Recalculate();
                    break;

                case LengthCoordinatorOutcomeKind.Empty:
                    _cadSelectionResult = null;
                    SourceType = MeasurementSourceType.CadSelection;
                    Recalculate();
                    StatusText = "선택된 객체가 없습니다.";
                    break;

                case LengthCoordinatorOutcomeKind.Cancelled:
                    _isCancelled = true;
                    OnPropertyChanged(nameof(IsCancelled));
                    StatusText = "선택이 취소되었습니다.";
                    break;

                case LengthCoordinatorOutcomeKind.Error:
                    ApplyIpcError(outcome.Error!);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Length source selection failed");
            _isError = true;
            OnPropertyChanged(nameof(IsError));
            StatusText = "기준선을 선택하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyIpcError(IpcError error)
    {
        _isError = true;
        OnPropertyChanged(nameof(IsError));
        StatusText = error.Code switch
        {
            IpcErrorCode.NoActiveDocument => "열려 있는 도면이 없습니다.",
            IpcErrorCode.Timeout => "AutoCAD 응답이 지연되고 있습니다.\n\n잠시 후 다시 시도해주세요.",
            _ => "기준선을 선택하지 못했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요."
        };
    }

    private (double? Meters, string? DrawingName, IReadOnlyList<string> Handles, string? UnavailableReason) ResolveSource()
    {
        switch (SourceType)
        {
            case MeasurementSourceType.CadSelection:
                return FromLengthResult(_cadSelectionResult);

            case MeasurementSourceType.ExistingMeasurement:
                return FromLengthResult(_lengthWorkflow.LastResult);

            case MeasurementSourceType.Manual:
                if (string.IsNullOrWhiteSpace(ManualLengthText))
                {
                    return (null, null, Array.Empty<string>(), null);
                }

                if (!double.TryParse(ManualLengthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw))
                {
                    return (null, null, Array.Empty<string>(), "유효한 숫자를 입력해주세요.");
                }

                if (raw <= 0)
                {
                    return (null, null, Array.Empty<string>(), "길이는 0보다 커야 합니다.");
                }

                var factor = DrawingUnitConversion.MetersPerUnit(ManualLengthUnit);
                return factor is null
                    ? (null, null, Array.Empty<string>(), "지원하지 않는 단위입니다.")
                    : (raw * factor.Value, "Manual Entry", Array.Empty<string>(), null);

            default:
                return (null, null, Array.Empty<string>(), null);
        }
    }

    private static (double? Meters, string? DrawingName, IReadOnlyList<string> Handles, string? UnavailableReason) FromLengthResult(
        LengthMeasurementResult? result)
    {
        if (result is null)
        {
            return (null, null, Array.Empty<string>(), null);
        }

        // §83: 기존 Length Measurement가 Unitless이면 자동으로 계산을 진행하지 않는다.
        if (result.DisplayValueMeters is not { } meters)
        {
            return (null, result.DrawingName, Array.Empty<string>(), "기준 길이 단위를 확인할 수 없습니다.");
        }

        return (meters, result.DrawingName, result.Objects.Select(o => o.Handle).ToList(), null);
    }

    private void Recalculate()
    {
        var (lengthMeters, drawingName, handles, unavailableReason) = ResolveSource();
        _lengthMeters = lengthMeters;
        _drawingName = drawingName;
        _objectHandles = handles;

        if (lengthMeters is null)
        {
            StatusText = unavailableReason ?? "기준 길이가 필요합니다.";
        }

        OnPropertyChanged(nameof(HasLength));
        OnPropertyChanged(nameof(LengthDisplay));
        OnPropertyChanged(nameof(DrawingName));
        OnPropertyChanged(nameof(LayerDisplay));
    }
}
