using System.Collections.ObjectModel;
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
    private string _selectedTool = "Length";
    private string _statusMessage = "Ready";

    public MainWindowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _connectionManager.PropertyChanged += OnConnectionManagerPropertyChanged;

        Length = new LengthWorkflowViewModel(connectionManager);
        Area = new AreaWorkflowViewModel(connectionManager);

        Navigation = new ObservableCollection<NavItem>
        {
            new("PROJECT", "Dashboard", "Alt+1", true) { IsSelected = true },
            new("PROJECT", "Files", "Alt+2"),
            new("CAD", "Drawing", "Alt+3", true),
            new("CAD", "Selection", "Alt+4"),
            new("CAD", "Layers", "Alt+5"),
            new("CAD", "Export", "Alt+6"),
            new("QUANTITY", "Length", "Ctrl+L", true),
            new("QUANTITY", "Area", "Ctrl+A"),
            new("QUANTITY", "Parapet", "Ctrl+R"),
            new("QUANTITY", "History", "Ctrl+H"),
            new("OUTPUT", "Plot", "Ctrl+P", true),
            new("OUTPUT", "PDF", "Ctrl+Shift+P"),
            new("OUTPUT", "Excel", "Ctrl+E"),
            new("SETTINGS", "Preferences", "Ctrl+,", true)
        };

        // Length/Area/Parapet 등은 Milestone 2+에서 구현한다 - 아래 데이터는 UI Shell을 보여주기 위한
        // 더미 데이터로 남겨둔다 (Milestone 1 §38: Selection/Length/Area는 이번 범위 밖).
        Metrics = new ObservableCollection<MetricItem>
        {
            new("Selected objects", "4", "3 polylines, 1 block reference", "Stable"),
            new("Total length", "255.941 m", "Layer A-WALL / metric units", "Result"),
            new("Area queue", "12", "2 drawings pending", "Working"),
            new("Last export", "09:42", "Excel report generated", "Success")
        };

        Drawings = new ObservableCollection<DrawingFile>
        {
            new("CWA_B1_FloorPlan_Architecture_Rev12.dwg", @"D:\Projects\Clarus\B1\CWA_B1_FloorPlan_Architecture_Rev12.dwg", "mm", DateTimeOffset.Now.AddMinutes(-18), true),
            new("CWA_B2_Structure_LongFileName_For_Overflow_Check_Rev03.dwg", @"D:\Projects\Clarus\B2\CWA_B2_Structure_LongFileName_For_Overflow_Check_Rev03.dwg", "mm", DateTimeOffset.Now.AddHours(-2), false),
            new("CWA_Site_Parapet_Area_2026-08.dwg", @"D:\Projects\Clarus\Site\CWA_Site_Parapet_Area_2026-08.dwg", "m", DateTimeOffset.Now.AddDays(-1), false)
        };

        QuantityRecords = new ObservableCollection<QuantityRecord>
        {
            new("Q-1024", "Length", "A-WALL", 4, 255.941m, "m", "CWA_B1_FloorPlan_Architecture_Rev12.dwg", DateTimeOffset.Now.AddMinutes(-9)),
            new("Q-1023", "Area", "A-FLOOR", 8, 118.420m, "m2", "CWA_B1_FloorPlan_Architecture_Rev12.dwg", DateTimeOffset.Now.AddMinutes(-17)),
            new("Q-1022", "Vertical Area", "A-PARAPET", 6, 42.136m, "m2", "CWA_Site_Parapet_Area_2026-08.dwg", DateTimeOffset.Now.AddHours(-1)),
            new("Q-1021", "Length", "S-BEAM", 14, 364.780m, "m", "CWA_B2_Structure_LongFileName_For_Overflow_Check_Rev03.dwg", DateTimeOffset.Now.AddHours(-2))
        };

        Activity = new ObservableCollection<OperationLogEntry>
        {
            new(DateTimeOffset.Now.AddMinutes(-1), "Info", "AutoCAD connection verified", "Active document: CWA_B1_FloorPlan_Architecture_Rev12.dwg"),
            new(DateTimeOffset.Now.AddMinutes(-4), "Success", "Length extraction completed", "4 objects measured on A-WALL"),
            new(DateTimeOffset.Now.AddMinutes(-6), "Warning", "Open polyline skipped", "Select a closed polyline before area calculation")
        };

        Length.RecordAdded += (_, record) => QuantityRecords.Insert(0, record);
        Area.RecordAdded += (_, record) => QuantityRecords.Insert(0, record);

        OpenCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = true);
        CloseCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = false);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorOpen = !IsInspectorOpen);
        SelectNavigationCommand = new RelayCommand(SelectNavigation);
        RunExtractionCommand = new RelayCommand(RunExtraction);
        CopyResultCommand = new RelayCommand(() => StatusMessage = "Copied latest quantity result to clipboard queue");

        _connectionManager.Start();
    }

    public ObservableCollection<NavItem> Navigation { get; }
    public ObservableCollection<MetricItem> Metrics { get; }
    public ObservableCollection<DrawingFile> Drawings { get; }
    public ObservableCollection<QuantityRecord> QuantityRecords { get; }
    public ObservableCollection<OperationLogEntry> Activity { get; }

    public LengthWorkflowViewModel Length { get; }
    public AreaWorkflowViewModel Area { get; }

    public ICommand OpenCommandPaletteCommand { get; }
    public ICommand CloseCommandPaletteCommand { get; }
    public ICommand ToggleInspectorCommand { get; }
    public ICommand SelectNavigationCommand { get; }
    public ICommand RunExtractionCommand { get; }
    public ICommand CopyResultCommand { get; }

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
                OnPropertyChanged(nameof(IsDashboardContentVisible));
            }
        }
    }

    /// <summary>Length/Area는 각자 자기만의 패널을 갖는다 (Milestone 2 §38, Milestone 3 §5) - 나머지는
    /// 아직 기존 Dashboard 콘텐츠를 공유한다.</summary>
    public bool IsLengthToolSelected => _selectedTool == "Length";

    public bool IsAreaToolSelected => _selectedTool == "Area";

    public bool IsDashboardContentVisible => !IsLengthToolSelected && !IsAreaToolSelected;

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
        CadConnectionState.Connected => "BrushSuccess",
        CadConnectionState.Connecting or CadConnectionState.Reconnecting => "BrushWarning",
        CadConnectionState.PluginUnavailable or CadConnectionState.Faulted or CadConnectionState.Disconnected => "BrushError",
        _ => "BrushTextMuted"
    };

    private void OnConnectionManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // ConnectionManager는 자기 프로퍼티만 안다 (State/Instance/Drawing) - 여기서 우리 프로퍼티로 옮겨 알린다.
        OnPropertyChanged(nameof(ConnectionState));
        OnPropertyChanged(nameof(ConnectionLabel));
        OnPropertyChanged(nameof(ActiveDrawing));
        OnPropertyChanged(nameof(Units));
        OnPropertyChanged(nameof(ConnectionStatusBrush));
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
    }

    private void RunExtraction()
    {
        StatusMessage = "Analyzing selected polylines: 4 objects detected";
        Activity.Insert(0, new OperationLogEntry(DateTimeOffset.Now, "Info", "Length extraction queued", "Selection set contains 4 objects"));
    }
}
