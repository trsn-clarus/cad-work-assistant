using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Verification;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly IProjectContextService _projectContext;
    private readonly IQuantityVerificationCoordinator _verificationCoordinator;

    private bool _isCommandPaletteOpen;
    private bool _isInspectorOpen = true;
    private string _commandQuery = string.Empty;
    // "Dashboard" NavItem이 컬렉션에서 IsSelected=true로 시작하므로 여기도 맞춘다 - 실제 렌더링에서
    // 발견한 버그: 이 값이 "Length"였을 때 사이드바는 Dashboard가 선택된 것처럼 강조 표시되면서
    // 정작 보이는 화면은 Length 패널이었다 (Milestone 4.5).
    private string _selectedTool = "Dashboard";
    private string _statusMessage = "Ready";

    public MainWindowViewModel(
        IAutoCadConnectionManager connectionManager,
        IProjectContextService projectContext,
        IQuantityVerificationCoordinator verificationCoordinator,
        string dataFolderPath)
    {
        _connectionManager = connectionManager;
        _projectContext = projectContext;
        _verificationCoordinator = verificationCoordinator;
        _connectionManager.PropertyChanged += OnConnectionManagerPropertyChanged;

        Length = new LengthWorkflowViewModel(connectionManager);
        Area = new AreaWorkflowViewModel(connectionManager);
        VerticalArea = new VerticalAreaWorkflowViewModel(connectionManager, Length);
        Parapet = new ParapetWorkflowViewModel(connectionManager, Length);
        Drawing = new DrawingWorkflowViewModel(connectionManager);
        History = new QuantityHistoryViewModel(projectContext, verificationCoordinator);
        Settings = new SettingsViewModel(dataFolderPath);

        // 실제로 화면이 있는 항목만 Navigation에 올린다(Milestone 8 §29) - 이전에는 미구현 항목도
        // 자리만 예약해 비활성 표시했지만(Milestone 4.5 §23), 상용 제품 첫인상에서는 클릭할 수 없는
        // 항목이 5개나 보이는 쪽이 더 나쁘다는 판단으로 바꿨다. Files/Plot/PDF/Excel처럼 아직 화면이
        // 없는 기능은 실제로 생길 때 그 Milestone에서 추가한다.
        Navigation = new ObservableCollection<NavItem>
        {
            new("PROJECT", "Dashboard", "Alt+1", true) { IsSelected = true },
            // Selection/Layers/Export는 별도 화면으로 쪼개지 않고 Drawing 워크스페이스 하나에 통합했다
            // (§18 "초기 구현에서 너무 많은 페이지로 쪼개지 않아도 된다") - 그래서 각각의 자리를 따로
            // 예약해두지 않는다.
            new("CAD", "Drawing", "Alt+3", true),
            new("QUANTITY", "Length", "Ctrl+L", true),
            new("QUANTITY", "Area", "Ctrl+A"),
            new("QUANTITY", "Vertical Area", "Ctrl+V"),
            new("QUANTITY", "Parapet", "Ctrl+R"),
            new("QUANTITY", "History", "Ctrl+H"),
            new("SETTINGS", "Settings", "Ctrl+,", true)
        };

        // 산출내역/활동은 더 이상 이 ViewModel이 직접 소유하지 않는다 - IProjectContextService가
        // 프로젝트별로 관리하고(Milestone 6), 여기서는 그 컬렉션을 그대로 노출한다(QuantityRecords/
        // Activity 프로퍼티). 세션에서 실제로 추가한 값만 보인다는 원칙은 그대로다 - 가짜 샘플 행을
        // 미리 채우지 않는다(Milestone 4.5 §44-45). 비어 있을 때는 XAML의 Empty State 문구가 안내한다.
        _projectContext.QuantityRecords.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasQuantityRecords));
            RefreshInspector();
        };

        _projectContext.Activity.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasActivity));
            RefreshInspector();
        };

        _projectContext.CurrentProjectChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentProjectName));
            OnPropertyChanged(nameof(HasCurrentProject));
        };

        InspectorRows = new ObservableCollection<InspectorRow>();

        async void OnRecordAdded(object? sender, QuantityRecord record)
        {
            try
            {
                await _projectContext.AddQuantityRecordAsync(
                    record,
                    $"{record.Type} 산출내역 추가",
                    $"{record.Layer} · {record.ObjectCount}개 객체 · {record.Value:N3} {record.Unit}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AddQuantityRecordAsync failed");
                StatusMessage = "산출내역을 저장하지 못했습니다. 잠시 후 다시 시도해주세요.";
                return;
            }

            // §48: 새 산출내역을 추가하면 빠른 결정적 검산을 자동 실행한다 - 측정 도구는 여전히
            // Verification을 전혀 모른다(Project를 모르는 것과 같은 이유), 여기서만 조립한다.
            try
            {
                var allRecords = _projectContext.QuantityRecords.ToList();
                await _verificationCoordinator.VerifyAsync(record, allRecords);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Automatic verification after AddQuantityRecordAsync failed");
            }
        }

        Length.RecordAdded += OnRecordAdded;
        Area.RecordAdded += OnRecordAdded;
        VerticalArea.RecordAdded += OnRecordAdded;
        Parapet.RecordAdded += OnRecordAdded;

        async void OnExportCompleted(object? sender, ExportCompletedEventArgs e)
        {
            try
            {
                await _projectContext.AddExportRecordAsync(e.SourceDrawing, e.TargetFile, e.ObjectCount, e.Description);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AddExportRecordAsync failed");
            }
        }

        Drawing.Export.ExportCompleted += OnExportCompleted;

        // Property Inspector는 지금 활성화된 도구의 실제 상태를 그대로 비춘다 (Milestone 4.5 §9,
        // "실제 도구로 구현 - 활성 도구별 동적 바인딩"). 각 Workflow VM/그 Rows 컬렉션/기준 길이를
        // 다루는 LengthSourceSelector가 바뀔 때마다 다시 그린다.
        Length.PropertyChanged += (_, _) => RefreshInspector();
        Length.Rows.CollectionChanged += (_, _) => RefreshInspector();
        Area.PropertyChanged += (_, _) => RefreshInspector();
        Area.Rows.CollectionChanged += (_, _) => RefreshInspector();
        VerticalArea.PropertyChanged += (_, _) => RefreshInspector();
        VerticalArea.Source.PropertyChanged += (_, _) => RefreshInspector();
        Parapet.PropertyChanged += (_, _) => RefreshInspector();
        Parapet.Source.PropertyChanged += (_, _) => RefreshInspector();
        Drawing.PropertyChanged += (_, _) => RefreshInspector();
        Drawing.Rows.CollectionChanged += (_, _) => RefreshInspector();
        History.PropertyChanged += (_, _) => RefreshInspector();

        OpenCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = true);
        CloseCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = false);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorOpen = !IsInspectorOpen);
        SelectNavigationCommand = new RelayCommand(SelectNavigation);
        OpenProjectDialogCommand = new RelayCommand(OpenProjectDialog);

        RefreshInspector();
        _connectionManager.Start();

        InitializeProjectContext();
    }

    public ObservableCollection<NavItem> Navigation { get; }
    public ObservableCollection<QuantityRecord> QuantityRecords => _projectContext.QuantityRecords;
    public ObservableCollection<ActivityRecord> Activity => _projectContext.Activity;
    public ObservableCollection<InspectorRow> InspectorRows { get; }

    public bool HasQuantityRecords => QuantityRecords.Count > 0;

    public bool HasActivity => Activity.Count > 0;

    /// <summary>Project가 열려 있지 않으면 "빠른 세션"이다 (§21) - 사이드바가 이 값으로 안내 문구를
    /// 바꾼다.</summary>
    public string CurrentProjectName => _projectContext.CurrentProject?.Name ?? "빠른 세션 (프로젝트 없음)";

    public bool HasCurrentProject => _projectContext.CurrentProject is not null;

    /// <summary>Dashboard 요약용 - 검산 결과가 "확인 필요"인 산출내역 건수(§105-106). 거대한 KPI
    /// 카드로 되돌아가지 않는다 - Inspector의 한 줄로 충분하다.</summary>
    public int ReviewNeededCount => History.Rows.Count(r => r.Verification?.OverallSeverity == VerificationSeverity.Review);

    public LengthWorkflowViewModel Length { get; }
    public AreaWorkflowViewModel Area { get; }
    public VerticalAreaWorkflowViewModel VerticalArea { get; }
    public ParapetWorkflowViewModel Parapet { get; }
    public DrawingWorkflowViewModel Drawing { get; }
    public QuantityHistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    public ICommand OpenCommandPaletteCommand { get; }
    public ICommand CloseCommandPaletteCommand { get; }
    public ICommand ToggleInspectorCommand { get; }
    public ICommand SelectNavigationCommand { get; }
    public ICommand OpenProjectDialogCommand { get; }

    /// <summary>ViewModel은 Window를 직접 만들지 않는다 - MainWindow.xaml.cs가 구독해서 대화상자를
    /// 띄운다 (ProjectDialogViewModel.RequestClose와 같은 패턴).</summary>
    public event EventHandler? RequestOpenProjectDialog;

    public bool IsCommandPaletteOpen
    {
        get => _isCommandPaletteOpen;
        set => SetProperty(ref _isCommandPaletteOpen, value);
    }

    public bool IsInspectorOpen
    {
        get => _isInspectorOpen;
        set => SetProperty(ref _isInspectorOpen, value);
    }

    public string CommandQuery
    {
        get => _commandQuery;
        set => SetProperty(ref _commandQuery, value);
    }

    public string SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value))
            {
                OnPropertyChanged(nameof(IsLengthToolSelected));
                OnPropertyChanged(nameof(IsAreaToolSelected));
                OnPropertyChanged(nameof(IsVerticalAreaToolSelected));
                OnPropertyChanged(nameof(IsParapetToolSelected));
                OnPropertyChanged(nameof(IsDrawingToolSelected));
                OnPropertyChanged(nameof(IsHistoryToolSelected));
                OnPropertyChanged(nameof(IsSettingsToolSelected));
                OnPropertyChanged(nameof(IsDashboardContentVisible));
                RefreshInspector();
            }
        }
    }

    /// <summary>Length/Area/Vertical Area/Parapet는 각자 자기만의 패널을 갖는다 (Milestone 2 §38,
    /// Milestone 3 §5, Milestone 4 §42) - 나머지는 아직 기존 Dashboard 콘텐츠를 공유한다.</summary>
    public bool IsLengthToolSelected => _selectedTool == "Length";

    public bool IsAreaToolSelected => _selectedTool == "Area";

    public bool IsVerticalAreaToolSelected => _selectedTool == "Vertical Area";

    public bool IsParapetToolSelected => _selectedTool == "Parapet";

    public bool IsDrawingToolSelected => _selectedTool == "Drawing";

    public bool IsHistoryToolSelected => _selectedTool == "History";

    public bool IsSettingsToolSelected => _selectedTool == "Settings";

    public bool IsDashboardContentVisible =>
        !IsLengthToolSelected && !IsAreaToolSelected && !IsVerticalAreaToolSelected && !IsParapetToolSelected
        && !IsDrawingToolSelected && !IsHistoryToolSelected && !IsSettingsToolSelected;

    public string InspectorTitle => _selectedTool switch
    {
        "Length" => "Length Selection",
        "Area" => "Area Selection",
        "Vertical Area" => "Vertical Area",
        "Parapet" => "Parapet",
        "Drawing" => "Drawing Navigation",
        "History" => "Quantity History",
        "Settings" => "Settings",
        _ => "Session"
    };

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // --- 아래는 전부 IAutoCadConnectionManager의 실시간 값을 그대로 노출하는 pass-through 프로퍼티다.
    // 기존 XAML 바인딩 경로(ConnectionLabel/ActiveDrawing/Units)를 그대로 유지해 UI를 다시 그리지 않는다 (§3, §33).

    public CadConnectionState ConnectionState => _connectionManager.State;

    public string ConnectionLabel => _connectionManager.State switch
    {
        CadConnectionState.NoAutoCadProcess => "AutoCAD Not Running",
        CadConnectionState.ProcessDetected => "AutoCAD Detected · Select Instance",
        CadConnectionState.PluginUnavailable => "AutoCAD Detected · Plugin Not Loaded",
        CadConnectionState.Connecting => "Connecting…",
        CadConnectionState.Connected => _connectionManager.Instance is { } info
            ? (info.IsSimulated ? $"[SIMULATION] {info.Product} Connected" : $"{info.Product} Connected")
            : "AutoCAD Connected",
        CadConnectionState.Reconnecting => "Reconnecting…",
        CadConnectionState.Disconnected => "AutoCAD Disconnected",
        CadConnectionState.Faulted => "AutoCAD Connection Error",
        _ => "Unknown"
    };

    public string ActiveDrawing => _connectionManager.Drawing?.DocumentDisplayName
        ?? (_connectionManager.State == CadConnectionState.Connected ? "No document open" : "—");

    public string Units => _connectionManager.Drawing?.Units is { } unit ? DrawingUnitDisplay.Abbreviation(unit) : "—";

    public string SelectionSummary => "No selection";

    public Brush ConnectionStatusBrush =>
        (Brush?)Application.Current?.TryFindResource(ConnectionStatusBrushKey) ?? Brushes.Gray;

    private string ConnectionStatusBrushKey => _connectionManager.State switch
    {
        CadConnectionState.Connected => "BrushConnected",
        CadConnectionState.Connecting or CadConnectionState.Reconnecting => "BrushConnecting",
        CadConnectionState.PluginUnavailable or CadConnectionState.Faulted => "BrushConnectionError",
        CadConnectionState.Disconnected => "BrushConnectionError",
        _ => "BrushDisconnected"
    };

    /// <summary>
    /// 연결 상태를 색상에만 의존하지 않고 별도 기호로도 구분한다 (Milestone 4.5 §67-68, 색맹 사용자도
    /// 상태를 구분할 수 있어야 한다). ●=연결됨, ◐=진행 중, ◇=감지됨(대기), △=Plugin 없음, ✕=끊김, !=오류, ○=미실행.
    /// </summary>
    public string ConnectionStatusGlyph => _connectionManager.State switch
    {
        CadConnectionState.NoAutoCadProcess => "○",
        CadConnectionState.ProcessDetected => "◇",
        CadConnectionState.PluginUnavailable => "△",
        CadConnectionState.Connecting or CadConnectionState.Reconnecting => "◐",
        CadConnectionState.Connected => "●",
        CadConnectionState.Disconnected => "✕",
        CadConnectionState.Faulted => "!",
        _ => "○"
    };

    private void OnConnectionManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // ConnectionManager는 자기 프로퍼티만 안다 (State/Instance/Drawing) - 여기서 우리 프로퍼티로 옮겨 알린다.
        OnPropertyChanged(nameof(ConnectionState));
        OnPropertyChanged(nameof(ConnectionLabel));
        OnPropertyChanged(nameof(ActiveDrawing));
        OnPropertyChanged(nameof(Units));
        OnPropertyChanged(nameof(ConnectionStatusBrush));
        OnPropertyChanged(nameof(ConnectionStatusGlyph));
        RefreshInspector();
        TryRegisterActiveDrawing();
    }

    private string? _lastRegisteredDrawingPath;

    /// <summary>AutoCAD가 도면을 열 때마다 자동으로 프로젝트에 참조를 남긴다 (§34-37) - 파일을
    /// 복사하지 않고 경로만 기억한다. 같은 경로를 매 PropertyChanged마다 다시 등록하지 않도록
    /// 마지막으로 등록한 경로와 비교한다(연결 상태만 바뀌어도 이 핸들러가 자주 불린다).</summary>
    private async void TryRegisterActiveDrawing()
    {
        if (_connectionManager.Drawing?.FullPath is not { } fullPath || fullPath == _lastRegisteredDrawingPath)
        {
            return;
        }

        _lastRegisteredDrawingPath = fullPath;

        try
        {
            await _projectContext.RegisterDrawingFileAsync(
                fullPath,
                System.IO.Path.GetFileName(fullPath),
                DrawingUnitDisplay.Abbreviation(_connectionManager.Drawing.Units));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RegisterDrawingFileAsync failed");
        }
    }

    private async void InitializeProjectContext()
    {
        try
        {
            await _projectContext.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProjectContextService.InitializeAsync failed");
            StatusMessage = "프로젝트 데이터를 불러오지 못했습니다. 빠른 세션으로 계속합니다.";
        }
    }

    private void OpenProjectDialog()
    {
        RequestOpenProjectDialog?.Invoke(this, EventArgs.Empty);
    }

    private void SelectNavigation(object? parameter)
    {
        if (parameter is not NavItem selected)
        {
            return;
        }

        foreach (var item in Navigation)
        {
            item.IsSelected = ReferenceEquals(item, selected);
        }

        SelectedTool = selected.Label;
        StatusMessage = $"{selected.Label} workspace selected";

        if (selected.Label == "Drawing")
        {
            Drawing.OnActivated();
        }
        else if (selected.Label == "History")
        {
            History.OnActivated();
        }
    }

    private void RefreshInspector()
    {
        InspectorRows.Clear();

        switch (_selectedTool)
        {
            case "Length":
                InspectorRows.Add(new InspectorRow("상태", Length.StatusText));
                InspectorRows.Add(new InspectorRow("선택 객체", $"{Length.Rows.Count}개"));
                InspectorRows.Add(new InspectorRow("레이어", SummarizeLayers(Length.Rows.Select(r => r.Layer))));
                InspectorRows.Add(new InspectorRow("총 길이", Length.TotalDisplay ?? "—"));
                break;

            case "Area":
                InspectorRows.Add(new InspectorRow("상태", Area.StatusText));
                InspectorRows.Add(new InspectorRow("선택 객체", $"{Area.Rows.Count}개"));
                InspectorRows.Add(new InspectorRow("레이어", SummarizeLayers(Area.Rows.Select(r => r.Layer))));
                InspectorRows.Add(new InspectorRow("총 면적", Area.TotalDisplay ?? "—"));
                break;

            case "Vertical Area":
                InspectorRows.Add(new InspectorRow("상태", VerticalArea.StatusText));
                InspectorRows.Add(new InspectorRow("기준 길이", VerticalArea.Source.LengthDisplay ?? "—"));
                InspectorRows.Add(new InspectorRow("레이어", VerticalArea.Source.LayerDisplay));
                InspectorRows.Add(new InspectorRow("높이", FormatHeight(VerticalArea.HeightText, VerticalArea.HeightUnit)));
                InspectorRows.Add(new InspectorRow("총 수직면적", VerticalArea.TotalDisplay ?? "—"));
                break;

            case "Parapet":
                InspectorRows.Add(new InspectorRow("상태", Parapet.StatusText));
                InspectorRows.Add(new InspectorRow("기준 길이(둘레)", Parapet.Source.LengthDisplay ?? "—"));
                InspectorRows.Add(new InspectorRow("레이어", Parapet.Source.LayerDisplay));
                InspectorRows.Add(new InspectorRow("높이", FormatHeight(Parapet.HeightText, Parapet.HeightUnit)));
                InspectorRows.Add(new InspectorRow("면", Parapet.IsBothFaces ? "양면" : "한 면"));
                InspectorRows.Add(new InspectorRow("상부면", Parapet.TopIncluded ? "포함" : "미포함"));
                InspectorRows.Add(new InspectorRow("총 면적", Parapet.TotalDisplay ?? "—"));
                break;

            case "Drawing":
                InspectorRows.Add(new InspectorRow("상태", Drawing.StatusText));
                InspectorRows.Add(new InspectorRow("도면 개요", Drawing.OverviewText));
                InspectorRows.Add(new InspectorRow("선택 객체", $"{Drawing.Rows.Count}개"));
                InspectorRows.Add(new InspectorRow("격리 상태", Drawing.IsIsolationActive ? "격리됨" : "정상"));
                break;

            case "History":
                InspectorRows.Add(new InspectorRow("요약", History.SummaryText));
                InspectorRows.Add(new InspectorRow("선택된 기록", History.SelectedRow is { } row
                    ? $"{row.Type} · {row.Value:N3} {row.Unit}"
                    : "없음"));
                InspectorRows.Add(new InspectorRow("검토 상태", History.SelectedRow?.ReviewLabel ?? "—"));
                break;

            case "Settings":
                InspectorRows.Add(new InspectorRow("버전", Settings.VersionText));
                InspectorRows.Add(new InspectorRow("데이터 위치", Settings.DataFolderPath));
                break;

            default:
                InspectorRows.Add(new InspectorRow("프로젝트", CurrentProjectName));
                InspectorRows.Add(new InspectorRow("연결 상태", ConnectionLabel));
                InspectorRows.Add(new InspectorRow("활성 도면", ActiveDrawing));
                InspectorRows.Add(new InspectorRow("산출내역", $"{QuantityRecords.Count}건"));
                InspectorRows.Add(new InspectorRow("확인 필요", $"{ReviewNeededCount}건"));
                InspectorRows.Add(new InspectorRow("최근 활동", Activity.Count > 0 ? Activity[0].Title : "없음"));
                break;
        }

        OnPropertyChanged(nameof(InspectorTitle));
    }

    private static string SummarizeLayers(IEnumerable<string> layers)
    {
        var distinct = layers.Distinct().ToList();
        return distinct.Count switch
        {
            0 => "—",
            1 => distinct[0],
            _ => "Mixed"
        };
    }

    private static string FormatHeight(string text, DrawingUnit unit) =>
        string.IsNullOrWhiteSpace(text) ? "—" : $"{text} {DrawingUnitDisplay.Abbreviation(unit)}";
}
