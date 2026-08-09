using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Text;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// CAD > Text 화면 (Milestone 12 §60-71). "선택하지 않은 객체는 건드리지 않는다, 선택하지 않은
/// 속성은 변경하지 않는다, Batch는 all-or-nothing"(§157) 세 원칙을 UI 레벨에서도 그대로 지킨다 -
/// 체크박스로 실제 바꿀 속성만 명시적으로 고르게 한다(§67, 실수 방지).
/// </summary>
public sealed class TextWorkflowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly RelayCommand _selectCommand;
    private readonly RelayCommand _applyCommand;

    private IReadOnlyList<CadTextObjectDto> _selectedObjects = Array.Empty<CadTextObjectDto>();
    private IReadOnlyList<string> _excludedTypeNames = Array.Empty<string>();
    private IReadOnlyList<string> _layerNames = Array.Empty<string>();

    private TextObjectRow? _selectedRow;

    private bool _isBusy;
    private string _statusText = "AutoCAD에서 문자를 선택하거나 새로 작성하세요.";
    private bool _isError;
    private bool _isSuccess;

    private string _contentText = string.Empty;

    private bool _includeHeightChange;
    private string _heightText = string.Empty;

    private bool _includeLayerChange;
    private string? _selectedLayerName;

    private bool _includeColorChange;
    private CadColorDto _selectedColor = CadColorPalette.ByLayer;

    private bool _isCreateMode;

    public TextWorkflowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        Rows = new ObservableCollection<TextObjectRow>();
        Create = new TextCreateViewModel(connectionManager);
        ColorChoices = BuildColorChoices();

        _selectCommand = new RelayCommand(SelectFromCad, CanRunCommand);
        _applyCommand = new RelayCommand(async () => await ApplyChangesAsync(), CanApply);

        _connectionManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAutoCadConnectionManager.State))
            {
                RaiseAllCanExecuteChanged();
            }
        };
    }

    public TextCreateViewModel Create { get; }

    public ObservableCollection<TextObjectRow> Rows { get; }

    /// <summary>§62 - "편집"/"작성"을 별도 페이지로 쪼개지 않고 한 화면 안에서 compact
    /// segmented control로 전환한다.</summary>
    public bool IsEditMode
    {
        get => !_isCreateMode;
        set { if (value) { IsCreateMode = false; } }
    }

    public bool IsCreateMode
    {
        get => _isCreateMode;
        set
        {
            if (SetProperty(ref _isCreateMode, value))
            {
                OnPropertyChanged(nameof(IsEditMode));
            }
        }
    }

    public ICommand SelectCommand => _selectCommand;

    public ICommand ApplyCommand => _applyCommand;

    public bool IsConnected => _connectionManager.State == CadConnectionState.Connected;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseAllCanExecuteChanged();
                OnPropertyChanged(nameof(SelectButtonLabel));
                OnPropertyChanged(nameof(ApplyButtonLabel));
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

    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    public string SelectButtonLabel => IsBusy ? "선택 중..." : "CAD에서 문자 선택";

    public string ApplyButtonLabel => IsBusy ? "적용 중..." : "변경 적용";

    public bool HasSelection => _selectedObjects.Count > 0;

    public int SelectedCount => _selectedObjects.Count;

    public bool IsSingleSelection => _selectedObjects.Count == 1;

    public string SelectionSummaryText
    {
        get
        {
            if (_selectedObjects.Count == 0)
            {
                return "선택된 문자가 없습니다.";
            }

            var text = $"{_selectedObjects.Count}개 문자 선택됨";
            if (_excludedTypeNames.Count > 0)
            {
                text += $" · 지원되지 않는 객체 {_excludedTypeNames.Count}개 제외 ({string.Join(", ", _excludedTypeNames)})";
            }

            return text;
        }
    }

    /// <summary>테이블에서 클릭한 행 - Property Inspector가 이 행의 상세를 보여준다(History의
    /// SelectedRow와 같은 패턴).</summary>
    public TextObjectRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                OnPropertyChanged(nameof(HasSelectedRow));
            }
        }
    }

    public bool HasSelectedRow => SelectedRow is not null;

    // --- 배치 편집 컨트롤 (§66-68) ---

    /// <summary>단일 선택일 때만 편집 가능(§19) - 여러 객체를 한 문자열로 덮어쓰는 실수를 막는다.</summary>
    public string ContentText
    {
        get => _contentText;
        set
        {
            if (SetProperty(ref _contentText, value))
            {
                _applyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IncludeHeightChange
    {
        get => _includeHeightChange;
        set
        {
            if (SetProperty(ref _includeHeightChange, value))
            {
                _applyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string HeightText
    {
        get => _heightText;
        set
        {
            if (SetProperty(ref _heightText, value))
            {
                _applyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IncludeLayerChange
    {
        get => _includeLayerChange;
        set
        {
            if (SetProperty(ref _includeLayerChange, value))
            {
                _applyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<string> LayerNames
    {
        get => _layerNames;
        private set
        {
            _layerNames = value;
            OnPropertyChanged(nameof(LayerNames));
            Create.SetLayerNames(value);
        }
    }

    public string? SelectedLayerName
    {
        get => _selectedLayerName;
        set
        {
            if (SetProperty(ref _selectedLayerName, value))
            {
                _applyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IncludeColorChange
    {
        get => _includeColorChange;
        set
        {
            if (SetProperty(ref _includeColorChange, value))
            {
                _applyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<CadColorDto> ColorChoices { get; }

    public CadColorDto SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    // --- 혼합 값 요약 (§12-13, BatchPropertyAggregator) ---

    public string HeightSummaryText => Summarize(
        BatchPropertyAggregator.Aggregate(_selectedObjects, o => o.Height), v => $"{v:0.###}");

    public string LayerSummaryText => Summarize(
        BatchPropertyAggregator.Aggregate(_selectedObjects, o => o.LayerName), v => v);

    public string ColorSummaryText => Summarize(
        BatchPropertyAggregator.Aggregate(_selectedObjects, o => o.Color), v => v.DisplayName);

    public void OnActivated()
    {
        if (IsConnected && _layerNames.Count == 0)
        {
            _ = RefreshLayersAsync();
        }
    }

    private bool CanRunCommand() => !IsBusy && IsConnected;

    private async void SelectFromCad()
    {
        IsBusy = true;
        IsError = false;
        IsSuccess = false;
        StatusText = "AutoCAD에서 문자 객체를 선택해주세요...";
        RaiseAllCanExecuteChanged();

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.SelectTextObjects, payload: null, CancellationToken.None)
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
                    ApplyFailure(response.Error!, "문자 선택에 실패했습니다.");
                }

                return;
            }

            var selection = response.DeserializePayload<TextSelectionResponse>();
            if (selection is null)
            {
                ApplyError("문자 선택에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
                return;
            }

            SetSelection(selection.Objects, selection.ExcludedObjectTypeNames);
            StatusText = SelectionSummaryText;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SelectTextObjects failed");
            ApplyError("문자 선택에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private void SetSelection(IReadOnlyList<CadTextObjectDto> objects, IReadOnlyList<string> excludedTypeNames)
    {
        _selectedObjects = objects;
        _excludedTypeNames = excludedTypeNames;

        Rows.Clear();
        foreach (var obj in objects)
        {
            Rows.Add(new TextObjectRow(obj));
        }

        SelectedRow = null;

        // §157: 선택이 바뀌면 편집 컨트롤을 전부 초기화한다 - 이전 선택에서 체크해둔 내용이 새
        // 선택에 실수로 적용되지 않도록.
        ContentText = objects.Count == 1 ? objects[0].PlainText : string.Empty;
        IncludeHeightChange = false;
        HeightText = HeightSummaryTextRaw();
        IncludeLayerChange = false;
        SelectedLayerName = null;
        IncludeColorChange = false;
        SelectedColor = CadColorPalette.ByLayer;

        NotifySelectionChanged();
    }

    private string HeightSummaryTextRaw()
    {
        var state = BatchPropertyAggregator.Aggregate(_selectedObjects, o => o.Height);
        return state.Kind == BatchPropertyKind.Uniform ? $"{state.Value:0.###}" : string.Empty;
    }

    private bool CanApply() => !IsBusy && IsConnected && HasSelection && BuildPatch().HasAnyChange;

    private async System.Threading.Tasks.Task ApplyChangesAsync()
    {
        var patch = BuildPatch();
        if (!patch.HasAnyChange)
        {
            return;
        }

        if (patch.Height.HasValue && !TextHeightValidator.IsValid(patch.Height.Value))
        {
            StatusText = "높이는 0보다 커야 합니다.";
            IsError = true;
            return;
        }

        IsBusy = true;
        IsError = false;
        IsSuccess = false;
        StatusText = "변경 적용 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var handles = _selectedObjects.Select(o => o.Handle).ToList();
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.UpdateTextObjects, new UpdateTextObjectsRequest(handles, patch), CancellationToken.None)
                .ConfigureAwait(true);

            if (!response.Success)
            {
                ApplyFailure(response.Error!, "문자 변경에 실패했습니다.");
                return;
            }

            var result = response.DeserializePayload<UpdateTextObjectsResponse>();
            if (result is null)
            {
                ApplyError("문자 변경에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
                return;
            }

            SetSelection(result.UpdatedObjects, Array.Empty<string>());
            StatusText = $"문자 {result.UpdatedCount}개 수정 완료";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UpdateTextObjects failed");
            ApplyError("문자 변경에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.");
        }
        finally
        {
            IsBusy = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private TextUpdatePatch BuildPatch()
    {
        var content = IsSingleSelection && !string.Equals(ContentText, _selectedObjects[0].Content, StringComparison.Ordinal) && TextContentValidator.IsValid(ContentText)
            ? OptionalValue<string>.Some(ContentText)
            : OptionalValue<string>.None();

        var height = IncludeHeightChange && double.TryParse(HeightText, out var parsedHeight)
            ? OptionalValue<double>.Some(parsedHeight)
            : OptionalValue<double>.None();

        var layer = IncludeLayerChange && SelectedLayerName is not null
            ? OptionalValue<string>.Some(SelectedLayerName)
            : OptionalValue<string>.None();

        var color = IncludeColorChange
            ? OptionalValue<CadColorDto>.Some(SelectedColor)
            : OptionalValue<CadColorDto>.None();

        return new TextUpdatePatch(content, height, layer, color);
    }

    private async System.Threading.Tasks.Task RefreshLayersAsync()
    {
        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.GetLayers, payload: null, CancellationToken.None)
                .ConfigureAwait(true);

            if (!response.Success)
            {
                return;
            }

            var layers = response.DeserializePayload<GetLayersResponse>();
            if (layers is not null)
            {
                LayerNames = layers.Layers.Select(l => l.Name).ToList();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GetLayers failed for Text workspace");
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(IsSingleSelection));
        OnPropertyChanged(nameof(SelectionSummaryText));
        OnPropertyChanged(nameof(HeightSummaryText));
        OnPropertyChanged(nameof(LayerSummaryText));
        OnPropertyChanged(nameof(ColorSummaryText));
        _applyCommand.RaiseCanExecuteChanged();
    }

    private static string Summarize<T>(BatchPropertyState<T> state, Func<T, string> format) => state.Kind switch
    {
        BatchPropertyKind.Empty => "-",
        BatchPropertyKind.Mixed => "혼합",
        BatchPropertyKind.Uniform => format(state.Value!),
        _ => "-"
    };

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

    // §55: 잠긴 Layer/잘못된 handle 실패는 Handler가 이미 구체적인 설명을 만들어 보낸다
    // (예: "'A-LOCKED' Layer가 잠겨 있어..." ) - 여기서 뭉뚱그리면 그 설명이 사용자에게 닿지 않는다.
    private static string DescribeError(IpcError error, string genericMessage) => error.Code switch
    {
        IpcErrorCode.NoActiveDocument => "열려 있는 도면이 없습니다.",
        IpcErrorCode.Timeout => "AutoCAD 응답이 지연되고 있습니다.\n\n잠시 후 다시 시도해주세요.",
        // ApiExecutionFailed는 Handler catch 블록의 raw AutoCAD 예외 메시지도 여기로 온다(CLAUDE.md
        // 절대 원칙 #4 - 원시 Exception 노출 금지) - InvalidRequest만 통과시킨다. Handler는 잠긴
        // Layer 등 "명확한 설명이 필요한" 실패를 전부 InvalidRequest로 분류해 보낸다.
        IpcErrorCode.InvalidRequest when !string.IsNullOrWhiteSpace(error.Message) => error.Message,
        _ => genericMessage + "\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요."
    };

    private void RaiseAllCanExecuteChanged()
    {
        _selectCommand.RaiseCanExecuteChanged();
        _applyCommand.RaiseCanExecuteChanged();
    }

    private static IReadOnlyList<CadColorDto> BuildColorChoices()
    {
        var choices = new List<CadColorDto> { CadColorPalette.ByLayer, CadColorPalette.ByBlock };
        choices.AddRange(CadColorPalette.CommonAci);
        return choices;
    }
}
