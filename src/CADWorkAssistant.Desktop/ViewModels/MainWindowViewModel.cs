using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;

namespace CADWorkAssistant.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;

    private bool _isCommandPaletteOpen;
    private bool _isInspectorOpen = true;
    private string _commandQuery = string.Empty;
    // "Dashboard" NavItem이 컬렉션에서 IsSelected=true로 시작하므로 여기도 맞춘다 - 실제 렌더링에서
    // 발견한 버그: 이 값이 "Length"였을 때 사이드바는 Dashboard가 선택된 것처럼 강조 표시되면서
    // 정작 보이는 화면은 Length 패널이었다 (Milestone 4.5).
    private string _selectedTool = "Dashboard";
    private string _statusMessage = "Ready";

    public MainWindowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _connectionManager.PropertyChanged += OnConnectionManagerPropertyChanged;

        Length = new LengthWorkflowViewModel(connectionManager);
        Area = new AreaWorkflowViewModel(connectionManager);
        VerticalArea = new VerticalAreaWorkflowViewModel(connectionManager, Length);
        Parapet = new ParapetWorkflowViewModel(connectionManager, Length);
        Drawing = new DrawingWorkflowViewModel(connectionManager);

        // 실제로 화면이 있는 항목만 isImplemented: true - 나머지는 자리만 예약해두고 비활성화한다
        // (§23 "미구현 기능을 버튼으로 과도하게 노출하지 않는다" - 완전히 숨기면 향후 기능이 붙을 자리를
        // 가늠할 수 없고, 그냥 활성화해두면 클릭했을 때 아무 일도 안 일어나거나 엉뚱한 화면이 보인다).
        // Drawing은 Milestone 5에서 실제 화면이 생겨 isImplemented: true로 전환했다.
        Navigation = new ObservableCollection<NavItem>
        {
            new("PROJECT", "Dashboard", "Alt+1", true) { IsSelected = true },
            new("PROJECT", "Files", "Alt+2", isImplemented: false),
            // Selection/Layers/Export는 별도 화면으로 쪼개지 않고 Drawing 워크스페이스 하나에 통합했다
            // (§18 "초기 구현에서 너무 많은 페이지로 쪼개지 않아도 된다") - 그래서 각각의 자리를 따로
            // 예약해두지 않는다.
            new("CAD", "Drawing", "Alt+3", true),
            new("QUANTITY", "Length", "Ctrl+L", true),
            new("QUANTITY", "Area", "Ctrl+A"),
            new("QUANTITY", "Vertical Area", "Ctrl+V"),
            new("QUANTITY", "Parapet", "Ctrl+R"),
            new("QUANTITY", "History", "Ctrl+H", isImplemented: false),
            new("OUTPUT", "Plot", "Ctrl+P", true, isImplemented: false),
            new("OUTPUT", "PDF", "Ctrl+Shift+P", isImplemented: false),
            new("OUTPUT", "Excel", "Ctrl+E", isImplemented: false),
            new("SETTINGS", "Preferences", "Ctrl+,", true, isImplemented: false)
        };

        // 세션에서 실제로 추가한 산출내역/활동만 보여준다 - 이전에는 화면을 채우려고 가짜 샘플 행을
        // 미리 넣어뒀는데, 실제 측정값과 섞이면 사용자가 가짜 행을 진짜로 오인할 위험이 있다
        // (Milestone 4.5 §44-45, "Dashboard는 진짜 Control Center여야 한다"). 비어 있을 때는
        // XAML의 Empty State 문구가 대신 안내한다.
        QuantityRecords = new ObservableCollection<QuantityRecord>();
        QuantityRecords.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasQuantityRecords));
            RefreshInspector();
        };

        Activity = new ObservableCollection<OperationLogEntry>();
        Activity.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasActivity));
            RefreshInspector();
        };

        InspectorRows = new ObservableCollection<InspectorRow>();

        void OnRecordAdded(object? sender, QuantityRecord record)
        {
            QuantityRecords.Insert(0, record);
            Activity.Insert(0, new OperationLogEntry(record.CreatedAt, "Success", $"{record.Type} 산출내역 추가",
                $"{record.Layer} · {record.ObjectCount}개 객체 · {record.Value:N3} {record.Unit}"));
        }

        Length.RecordAdded += OnRecordAdded;
        Area.RecordAdded += OnRecordAdded;
        VerticalArea.RecordAdded += OnRecordAdded;
        Parapet.RecordAdded += OnRecordAdded;

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

        OpenCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = true);
        CloseCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = false);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorOpen = !IsInspectorOpen);
        SelectNavigationCommand = new RelayCommand(SelectNavigation);

        RefreshInspector();
        _connectionManager.Start();
    }

    public ObservableCollection<NavItem> Navigation { get; }
    public ObservableCollection<QuantityRecord> QuantityRecords { get; }
    public ObservableCollection<OperationLogEntry> Activity { get; }
    public ObservableCollection<InspectorRow> InspectorRows { get; }

    public bool HasQuantityRecords => QuantityRecords.Count > 0;

    public bool HasActivity => Activity.Count > 0;

    public LengthWorkflowViewModel Length { get; }
    public AreaWorkflowViewModel Area { get; }
    public VerticalAreaWorkflowViewModel VerticalArea { get; }
    public ParapetWorkflowViewModel Parapet { get; }
    public DrawingWorkflowViewModel Drawing { get; }

    public ICommand OpenCommandPaletteCommand { get; }
    public ICommand CloseCommandPaletteCommand { get; }
    public ICommand ToggleInspectorCommand { get; }
    public ICommand SelectNavigationCommand { get; }

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

    public bool IsDashboardContentVisible =>
        !IsLengthToolSelected && !IsAreaToolSelected && !IsVerticalAreaToolSelected && !IsParapetToolSelected && !IsDrawingToolSelected;

    public string InspectorTitle => _selectedTool switch
    {
        "Length" => "Length Selection",
        "Area" => "Area Selection",
        "Vertical Area" => "Vertical Area",
        "Parapet" => "Parapet",
        "Drawing" => "Drawing Navigation",
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

            default:
                InspectorRows.Add(new InspectorRow("연결 상태", ConnectionLabel));
                InspectorRows.Add(new InspectorRow("활성 도면", ActiveDrawing));
                InspectorRows.Add(new InspectorRow("산출내역", $"{QuantityRecords.Count}건"));
                InspectorRows.Add(new InspectorRow("최근 활동", Activity.Count > 0 ? Activity[0].Message : "없음"));
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
