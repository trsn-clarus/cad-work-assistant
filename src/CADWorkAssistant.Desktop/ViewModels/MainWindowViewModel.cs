using System.Collections.ObjectModel;
using System.Windows.Input;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private bool _isCommandPaletteOpen;
    private bool _isInspectorOpen = true;
    private string _commandQuery = string.Empty;
    private string _selectedTool = "Length";
    private string _statusMessage = "Ready";
    private CadConnectionState _connectionState = CadConnectionState.Connected;

    public MainWindowViewModel()
    {
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

        OpenCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = true);
        CloseCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = false);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorOpen = !IsInspectorOpen);
        SelectNavigationCommand = new RelayCommand(SelectNavigation);
        RunExtractionCommand = new RelayCommand(RunExtraction);
        CopyResultCommand = new RelayCommand(() => StatusMessage = "Copied latest quantity result to clipboard queue");
    }

    public ObservableCollection<NavItem> Navigation { get; }
    public ObservableCollection<MetricItem> Metrics { get; }
    public ObservableCollection<DrawingFile> Drawings { get; }
    public ObservableCollection<QuantityRecord> QuantityRecords { get; }
    public ObservableCollection<OperationLogEntry> Activity { get; }

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
        set => SetProperty(ref _selectedTool, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public CadConnectionState ConnectionState
    {
        get => _connectionState;
        set => SetProperty(ref _connectionState, value);
    }

    public string ActiveDrawing => Drawings.FirstOrDefault(drawing => drawing.IsActive)?.FileName ?? "No drawing";
    public string Units => Drawings.FirstOrDefault(drawing => drawing.IsActive)?.Units ?? "mm";
    public string SelectionSummary => "4 objects selected";
    public string ConnectionLabel => ConnectionState.ToString();

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
