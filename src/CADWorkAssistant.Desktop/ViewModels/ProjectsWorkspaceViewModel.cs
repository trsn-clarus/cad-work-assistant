using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Milestone 13 - "Projects" 화면(PROJECT 그룹). Milestone 6의 ProjectDialog는 빠른 생성/전환용으로
/// 계속 쓰고(§5), 이 ViewModel은 프로젝트가 많아졌을 때 찾기/열람/편집/도면-출력물-활동 확인을
/// 전담한다. Coordinator 계층 없이 IProjectContextService만 직접 쓴다(Text/Drawing/History와 같은
/// 패턴, Plot과 달리 이 화면은 새 IPC를 만들지 않는다).
/// </summary>
public sealed class ProjectsWorkspaceViewModel : ObservableObject
{
    private const int ActivitySectionLimit = 20;
    private const int OutputSectionLimit = 200;

    private readonly IProjectContextService _projectContext;
    private readonly RelayCommand _saveMetadataCommand;
    private readonly RelayCommand _openProjectCommand;
    private readonly RelayCommand _viewQuantityCommand;
    private readonly RelayCommand _viewDrawingCommand;

    private string _searchText = string.Empty;
    private string _sortOption = SortByLastOpened;
    private ProjectRow? _selectedRow;
    private bool _isLoading;
    private bool _isBusy;
    private string? _statusText;
    private bool _isError;

    private string _editName = string.Empty;
    private string _editClient = string.Empty;
    private string _editSite = string.Empty;
    private string _editDescription = string.Empty;

    private ProjectQuantitySummary? _quantitySummary;

    /// <summary>LoadDetailAsync는 서로 다른 트리거(선택 변경, 도면 자동 등록 완료 알림)에서 겹쳐
    /// 호출될 수 있다 - 매 호출마다 값을 늘려서, 먼저 시작했지만 나중에 끝난 호출이 최신 결과를
    /// 덮어쓰지 않게 막는다(그렇지 않으면 Drawings/Outputs/Activity에 중복 행이 쌓인다 - Simulation
    /// Mode에서 실제로 재현됨).</summary>
    private int _detailLoadVersion;

    private const string SortByLastOpened = "최근 열기";
    private const string SortByName = "프로젝트명";
    private const string SortByCreatedAt = "생성일";

    public ProjectsWorkspaceViewModel(IProjectContextService projectContext)
    {
        _projectContext = projectContext;
        _projectContext.CurrentProjectChanged += async (_, _) => await RefreshAsync();

        Rows = new ObservableCollection<ProjectRow>();
        FilteredRows = new ObservableCollection<ProjectRow>();
        DrawingFiles = new ObservableCollection<DrawingFileRow>();
        Outputs = new ObservableCollection<OutputRecordRow>();
        RecentActivity = new ObservableCollection<ActivityRecord>();

        CreateProjectCommand = new RelayCommand(() => RequestOpenProjectDialog?.Invoke(this, EventArgs.Empty));
        _saveMetadataCommand = new RelayCommand(async () => await SaveMetadataAsync(), CanSaveMetadata);
        _openProjectCommand = new RelayCommand(async () => await OpenSelectedProjectAsync(), () => HasSelection && !IsSelectedCurrent && !_isBusy);
        RelinkDrawingCommand = new RelayCommand(param => _ = RelinkAsync(param as DrawingFileRow));
        OpenDrawingFolderCommand = new RelayCommand(param => OpenContainingFolder((param as DrawingFileRow)?.FullPath));
        OpenOutputFileCommand = new RelayCommand(param => OpenFile((param as OutputRecordRow)?.Source.TargetFile));
        OpenOutputFolderCommand = new RelayCommand(param => OpenContainingFolder((param as OutputRecordRow)?.Source.TargetFile));
        _viewQuantityCommand = new RelayCommand(async () => await ViewQuantityAsync(), () => HasSelection);
        _viewDrawingCommand = new RelayCommand(async () => await ViewDrawingAsync(), () => HasSelection);
    }

    public ObservableCollection<ProjectRow> Rows { get; }

    public ObservableCollection<ProjectRow> FilteredRows { get; }

    public ObservableCollection<DrawingFileRow> DrawingFiles { get; }

    public ObservableCollection<OutputRecordRow> Outputs { get; }

    public ObservableCollection<ActivityRecord> RecentActivity { get; }

    public IReadOnlyList<string> SortOptions { get; } = new[] { SortByLastOpened, SortByName, SortByCreatedAt };

    public event EventHandler? RequestOpenProjectDialog;

    public event EventHandler? RequestShowHistory;

    public event EventHandler? RequestShowDrawing;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string SortOption
    {
        get => _sortOption;
        set
        {
            if (SetProperty(ref _sortOption, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public ProjectRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsSelectedCurrent));
                LoadEditFieldsFromSelection();
                RaiseAllCanExecuteChanged();
                _ = LoadDetailAsync();
            }
        }
    }

    public bool HasSelection => _selectedRow is not null;

    public bool IsSelectedCurrent => _selectedRow is not null && _selectedRow.Id == _projectContext.CurrentProject?.Id;

    public string EditName
    {
        get => _editName;
        set
        {
            if (SetProperty(ref _editName, value))
            {
                _saveMetadataCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string EditClient
    {
        get => _editClient;
        set => SetProperty(ref _editClient, value);
    }

    public string EditSite
    {
        get => _editSite;
        set => SetProperty(ref _editSite, value);
    }

    public string EditDescription
    {
        get => _editDescription;
        set => SetProperty(ref _editDescription, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

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

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public bool HasRows => Rows.Count > 0;

    public bool HasDrawingFiles => DrawingFiles.Count > 0;

    public bool HasOutputs => Outputs.Count > 0;

    public bool HasRecentActivity => RecentActivity.Count > 0;

    public string SummaryText => Rows.Count == FilteredRows.Count
        ? $"{Rows.Count}개 프로젝트"
        : $"{FilteredRows.Count} / {Rows.Count}개 프로젝트";

    public string QuantitySummaryText => _quantitySummary is { } s
        ? $"전체 {s.Total}건 · 검토 완료 {s.VerifiedCount}건 · 확인 필요 {s.NeedsReviewCount}건 · 오류 {s.ErrorCount}건"
        : "—";

    public ICommand CreateProjectCommand { get; }

    public ICommand SaveMetadataCommand => _saveMetadataCommand;

    public ICommand OpenProjectCommand => _openProjectCommand;

    public ICommand RelinkDrawingCommand { get; }

    public ICommand OpenDrawingFolderCommand { get; }

    public ICommand OpenOutputFileCommand { get; }

    public ICommand OpenOutputFolderCommand { get; }

    public ICommand ViewQuantityCommand => _viewQuantityCommand;

    public ICommand ViewDrawingCommand => _viewDrawingCommand;

    public async void OnActivated() => await RefreshAsync();

    /// <summary>§19 - MainWindowViewModel이 도면 자동 등록을 마친 뒤 호출한다. 등록은
    /// CurrentProjectChanged와 같은 이벤트에 독립적으로 반응하는 비동기 작업이라 순서가 보장되지
    /// 않는다(먼저 도착한 RefreshAsync가 아직 등록되지 않은 상태를 읽어버릴 수 있다) - 등록이 실제로
    /// 끝난 뒤 명시적으로 다시 불러온다.</summary>
    public async Task RefreshCurrentDetailAsync()
    {
        if (_selectedRow is not null)
        {
            await LoadDetailAsync();
        }
    }

    private async Task RefreshAsync()
    {
        var previouslySelectedId = _selectedRow?.Id;
        IsLoading = true;
        try
        {
            var projects = await _projectContext.GetAllProjectsAsync();
            var counts = await _projectContext.GetDrawingFileCountsAsync();
            var currentProjectId = _projectContext.CurrentProject?.Id;

            Rows.Clear();
            foreach (var project in projects)
            {
                Rows.Add(new ProjectRow(project, counts.TryGetValue(project.Id, out var count) ? count : 0, project.Id == currentProjectId));
            }

            OnPropertyChanged(nameof(HasRows));
            ApplyFilterAndSort();

            if (previouslySelectedId is not null)
            {
                var match = Rows.FirstOrDefault(r => r.Id == previouslySelectedId);
                SelectedRow = match;
            }
            else
            {
                OnPropertyChanged(nameof(IsSelectedCurrent));
                RaiseAllCanExecuteChanged();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProjectsWorkspaceViewModel.RefreshAsync failed");
            StatusText = "프로젝트 목록을 불러오지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<ProjectRow> query = Rows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText;
            query = query.Where(r =>
                r.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                || (r.Client is not null && r.Client.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                || (r.Site is not null && r.Site.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        query = SortOption switch
        {
            SortByName => query.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            SortByCreatedAt => query.OrderByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.LastOpenedAt)
        };

        FilteredRows.Clear();
        foreach (var row in query)
        {
            FilteredRows.Add(row);
        }

        OnPropertyChanged(nameof(SummaryText));
    }

    private void LoadEditFieldsFromSelection()
    {
        _editName = _selectedRow?.Name ?? string.Empty;
        _editClient = _selectedRow?.Client ?? string.Empty;
        _editSite = _selectedRow?.Site ?? string.Empty;
        _editDescription = _selectedRow?.Source.Description ?? string.Empty;
        OnPropertyChanged(nameof(EditName));
        OnPropertyChanged(nameof(EditClient));
        OnPropertyChanged(nameof(EditSite));
        OnPropertyChanged(nameof(EditDescription));
    }

    /// <summary>§10, §17-41 - DRAWINGS/OUTPUTS/ACTIVITY/QUANTITY 전부를 한 번에 불러온다. 파일
    /// 존재 확인은 Task.Run으로 UI 스레드를 막지 않는다(§21-22, 네트워크 경로 대비).</summary>
    private async Task LoadDetailAsync()
    {
        var version = ++_detailLoadVersion;

        DrawingFiles.Clear();
        Outputs.Clear();
        RecentActivity.Clear();
        _quantitySummary = null;
        OnPropertyChanged(nameof(QuantitySummaryText));
        OnPropertyChanged(nameof(HasDrawingFiles));
        OnPropertyChanged(nameof(HasOutputs));
        OnPropertyChanged(nameof(HasRecentActivity));

        if (_selectedRow is not { } row)
        {
            return;
        }

        var projectId = row.Id;

        // 이 호출보다 나중에 시작된 LoadDetailAsync가 있으면(예: 선택 변경 직후 도면 자동 등록 완료
        // 알림이 거의 동시에 도착) 그 결과를 덮어쓰지 않는다 - 매 await 뒤에 버전과 선택된 프로젝트를
        // 함께 확인한다.
        bool IsStale() => version != _detailLoadVersion || _selectedRow?.Id != projectId;

        try
        {
            var drawings = await _projectContext.GetDrawingFilesAsync(projectId);
            if (IsStale())
            {
                return;
            }

            var existsChecks = await Task.WhenAll(drawings.Select(d => Task.Run(() => File.Exists(d.FullPath))));
            if (IsStale())
            {
                return;
            }

            for (var i = 0; i < drawings.Count; i++)
            {
                DrawingFiles.Add(new DrawingFileRow(drawings[i], existsChecks[i]));
            }

            OnPropertyChanged(nameof(HasDrawingFiles));

            var outputs = await _projectContext.GetExportRecordsAsync(projectId, OutputSectionLimit);
            if (IsStale())
            {
                return;
            }

            var outputExistsChecks = await Task.WhenAll(outputs.Select(o => Task.Run(() => File.Exists(o.TargetFile))));
            if (IsStale())
            {
                return;
            }

            for (var i = 0; i < outputs.Count; i++)
            {
                Outputs.Add(new OutputRecordRow(outputs[i], outputExistsChecks[i]));
            }

            OnPropertyChanged(nameof(HasOutputs));

            var activity = await _projectContext.GetActivityForProjectAsync(projectId, ActivitySectionLimit);
            if (IsStale())
            {
                return;
            }

            foreach (var entry in activity)
            {
                RecentActivity.Add(entry);
            }

            OnPropertyChanged(nameof(HasRecentActivity));

            var summary = await _projectContext.GetQuantitySummaryAsync(projectId);
            if (IsStale())
            {
                return;
            }

            _quantitySummary = summary;
            OnPropertyChanged(nameof(QuantitySummaryText));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProjectsWorkspaceViewModel.LoadDetailAsync failed for project {ProjectId}", projectId);
            StatusText = "프로젝트 상세 정보를 불러오지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
    }

    private bool CanSaveMetadata() => _selectedRow is not null && !string.IsNullOrWhiteSpace(EditName) && !IsBusy;

    private async Task SaveMetadataAsync()
    {
        if (_selectedRow is not { } row)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _projectContext.UpdateProjectAsync(
                row.Id,
                EditName.Trim(),
                NullIfEmpty(EditClient),
                NullIfEmpty(EditSite),
                NullIfEmpty(EditDescription));

            StatusText = "프로젝트 정보가 저장되었습니다.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UpdateProjectAsync failed for project {ProjectId}", row.Id);
            StatusText = "프로젝트 정보를 저장하지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenSelectedProjectAsync()
    {
        if (_selectedRow is not { } row)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _projectContext.OpenProjectAsync(row.Id);
            StatusText = $"'{row.Name}' 프로젝트를 열었습니다.";
            IsError = false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OpenProjectAsync failed for project {ProjectId}", row.Id);
            StatusText = "프로젝트를 열지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>§23-26 - 새 경로로 다시 연결. 과거 산출내역의 SourceDrawing snapshot은 건드리지
    /// 않는다(§25, ProjectDataService.RelinkDrawingFileAsync가 DrawingFile 행 하나만 갱신).
    /// 파일명이 달라져도 자동으로 막지 않고(§26) 결과 메시지에 그대로 보여준다.</summary>
    private async Task RelinkAsync(DrawingFileRow? target)
    {
        if (target is null || _selectedRow is not { } row)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "AutoCAD 도면 (*.dwg)|*.dwg|모든 파일 (*.*)|*.*",
            Title = "다시 연결할 도면 파일 선택",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newFileName = Path.GetFileName(dialog.FileName);
        var previousFileName = target.FileName;

        try
        {
            await _projectContext.RelinkDrawingFileAsync(row.Id, target.Id, dialog.FileName, newFileName, target.Source.DrawingUnit);

            StatusText = string.Equals(previousFileName, newFileName, StringComparison.OrdinalIgnoreCase)
                ? $"'{newFileName}'(으)로 다시 연결했습니다."
                : $"'{previousFileName}' → '{newFileName}'(으)로 다시 연결했습니다.";
            IsError = false;

            await LoadDetailAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RelinkDrawingFileAsync failed for drawing {DrawingFileId}", target.Id);
            StatusText = "도면 파일을 다시 연결하지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
    }

    /// <summary>§42 - 압축된 요약(전체 건수만)에서 실제 편집 가능한 전체 History 화면으로 이동한다.
    /// 지금 보고 있는 프로젝트가 CurrentProject가 아니면 먼저 연다 - 그렇지 않으면 History가 다른
    /// 프로젝트의 기록을 보여주게 된다.</summary>
    private async Task ViewQuantityAsync()
    {
        if (_selectedRow is not { } row)
        {
            return;
        }

        if (!IsSelectedCurrent)
        {
            await OpenSelectedProjectAsync();
        }

        RequestShowHistory?.Invoke(this, EventArgs.Empty);
    }

    private async Task ViewDrawingAsync()
    {
        if (_selectedRow is not { } row)
        {
            return;
        }

        if (!IsSelectedCurrent)
        {
            await OpenSelectedProjectAsync();
        }

        RequestShowDrawing?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseAllCanExecuteChanged()
    {
        _saveMetadataCommand.RaiseCanExecuteChanged();
        _openProjectCommand.RaiseCanExecuteChanged();
        _viewQuantityCommand.RaiseCanExecuteChanged();
        _viewDrawingCommand.RaiseCanExecuteChanged();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>§29 - DWG 자체는 CAD Work Assistant가 직접 열지 않는다(AutoCAD 연결 없이 Shell
    /// association으로 열리면 예기치 않은 프로그램이 뜰 수 있다) - 폴더만 연다.</summary>
    private static void OpenContainingFolder(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        var folder = Path.GetDirectoryName(filePath);
        if (folder is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OpenContainingFolder failed for {Path}", filePath);
        }
    }

    /// <summary>§36 - Output(Excel/PDF/도면 PDF)은 곧바로 열어 확인하는 것이 목적이라 Shell
    /// association으로 직접 연다(ExcelExportViewModel/PdfExportViewModel과 같은 패턴).</summary>
    private static void OpenFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OpenFile failed for {Path}", filePath);
        }
    }
}
