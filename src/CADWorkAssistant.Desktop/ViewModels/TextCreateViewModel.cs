using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Text;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Milestone 12 §35-37, §73-75 - "새 문자 작성" 모드. 좌표를 숫자로 입력받지 않는다(§36) - 항상
/// AcquireTextInsertionPoint로 AutoCAD에서 실제 점을 받은 뒤에만 CreateText를 보낸다.
/// </summary>
public sealed class TextCreateViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly RelayCommand _createCommand;

    private CadTextEntityType _entityType = CadTextEntityType.SingleLine;
    private string _content = string.Empty;
    private string _heightText = "250";
    private string? _selectedLayerName;
    private CadColorDto _selectedColor = CadColorPalette.ByLayer;

    private bool _isAcquiringPoint;
    private bool _isCreating;
    private bool _isSuccess;
    private bool _isError;
    private string? _statusText;
    private string? _lastCreatedContent;

    public TextCreateViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        ColorChoices = BuildColorChoices();

        _createCommand = new RelayCommand(async () => await CreateAsync(), CanCreate);

        _connectionManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IAutoCadConnectionManager.State))
            {
                _createCommand.RaiseCanExecuteChanged();
            }
        };
    }

    public ICommand CreateCommand => _createCommand;

    public bool IsConnected => _connectionManager.State == CadConnectionState.Connected;

    public bool IsSingleLine
    {
        get => _entityType == CadTextEntityType.SingleLine;
        set { if (value) { EntityType = CadTextEntityType.SingleLine; } }
    }

    public bool IsMultiLine
    {
        get => _entityType == CadTextEntityType.MultiLine;
        set { if (value) { EntityType = CadTextEntityType.MultiLine; } }
    }

    public CadTextEntityType EntityType
    {
        get => _entityType;
        private set
        {
            if (SetProperty(ref _entityType, value))
            {
                OnPropertyChanged(nameof(IsSingleLine));
                OnPropertyChanged(nameof(IsMultiLine));
            }
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                _createCommand.RaiseCanExecuteChanged();
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
                _createCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<string> LayerNames { get; private set; } = Array.Empty<string>();

    /// <summary>null이면 현재 Layer를 쓴다(§34) - Desktop이 미리 조회해서 채우지 않는다.</summary>
    public string? SelectedLayerName
    {
        get => _selectedLayerName;
        set => SetProperty(ref _selectedLayerName, value);
    }

    public IReadOnlyList<CadColorDto> ColorChoices { get; }

    public CadColorDto SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    public bool IsAcquiringPoint
    {
        get => _isAcquiringPoint;
        private set
        {
            if (SetProperty(ref _isAcquiringPoint, value))
            {
                _createCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ActionButtonLabel));
            }
        }
    }

    public bool IsCreating
    {
        get => _isCreating;
        private set
        {
            if (SetProperty(ref _isCreating, value))
            {
                _createCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ActionButtonLabel));
            }
        }
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string? LastCreatedContent
    {
        get => _lastCreatedContent;
        private set => SetProperty(ref _lastCreatedContent, value);
    }

    /// <summary>단일 버튼이 "위치 지정 → 작성" 두 단계를 순서대로 수행한다(§37) - 지금 어느
    /// 단계인지 라벨로 그대로 보여준다.</summary>
    public string ActionButtonLabel
    {
        get
        {
            if (IsAcquiringPoint)
            {
                return "위치 지정 중...";
            }

            if (IsCreating)
            {
                return "작성 중...";
            }

            return "CAD에서 위치 지정 후 작성";
        }
    }

    public void SetLayerNames(IReadOnlyList<string> layerNames)
    {
        LayerNames = layerNames;
        OnPropertyChanged(nameof(LayerNames));
    }

    private bool CanCreate() =>
        IsConnected && !IsAcquiringPoint && !IsCreating && TextContentValidator.IsValid(Content) &&
        double.TryParse(HeightText, out var height) && TextHeightValidator.IsValid(height);

    private async System.Threading.Tasks.Task CreateAsync()
    {
        if (!double.TryParse(HeightText, out var height) || !TextHeightValidator.IsValid(height))
        {
            StatusText = "높이는 0보다 커야 합니다.";
            IsError = true;
            return;
        }

        IsAcquiringPoint = true;
        IsSuccess = false;
        IsError = false;
        StatusText = "AutoCAD에서 위치를 지정해주세요...";

        CadPointDto point;
        try
        {
            var pointResponse = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.AcquireTextInsertionPoint, payload: null, CancellationToken.None)
                .ConfigureAwait(true);

            if (!pointResponse.Success)
            {
                if (pointResponse.Error!.Code == IpcErrorCode.SelectionCancelled)
                {
                    // §75: 위치 지정 중 Esc - 문자 생성 없음, Error 상태 아님(오류가 아니라 정상적인 사용자 조작).
                    StatusText = "위치 지정이 취소되었습니다.";
                    return;
                }

                StatusText = DescribeError(pointResponse.Error!, "위치 지정에 실패했습니다.");
                IsError = true;
                return;
            }

            point = pointResponse.DeserializePayload<AcquireTextInsertionPointResponse>()!.Point;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AcquireTextInsertionPoint failed");
            StatusText = "위치 지정에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.";
            IsError = true;
            return;
        }
        finally
        {
            IsAcquiringPoint = false;
        }

        IsCreating = true;
        StatusText = "문자 작성 중...";

        try
        {
            var request = new CreateTextRequest(
                EntityType,
                Content,
                height,
                SelectedLayerName,
                SelectedColor.Equals(CadColorPalette.ByLayer) ? null : SelectedColor,
                point);

            var createResponse = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.CreateText, request, CancellationToken.None)
                .ConfigureAwait(true);

            if (!createResponse.Success)
            {
                StatusText = DescribeError(createResponse.Error!, "문자 작성에 실패했습니다.");
                IsError = true;
                return;
            }

            var created = createResponse.DeserializePayload<CreateTextResponse>()!.Created;
            LastCreatedContent = created.PlainText;
            StatusText = "문자 작성 완료";
            IsSuccess = true;
            Content = string.Empty;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CreateText failed");
            StatusText = "문자 작성에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsCreating = false;
        }
    }

    // §55: 잠긴 Layer 등은 Handler가 InvalidRequest로 구체적인 설명을 이미 만들어 보낸다.
    // ApiExecutionFailed는 raw AutoCAD 예외 메시지일 수 있어(CLAUDE.md 절대 원칙 #4) 그대로 보여주지 않는다.
    private static string DescribeError(IpcError error, string genericMessage) => error.Code switch
    {
        IpcErrorCode.NoActiveDocument => "열려 있는 도면이 없습니다.",
        IpcErrorCode.Timeout => "AutoCAD 응답이 지연되고 있습니다.\n\n잠시 후 다시 시도해주세요.",
        IpcErrorCode.InvalidRequest when !string.IsNullOrWhiteSpace(error.Message) => error.Message,
        _ => genericMessage + "\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요."
    };

    private static IReadOnlyList<CadColorDto> BuildColorChoices()
    {
        var choices = new List<CadColorDto> { CadColorPalette.ByLayer, CadColorPalette.ByBlock };
        choices.AddRange(CadColorPalette.CommonAci);
        return choices;
    }
}
