using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using CADWorkAssistant.Documents.Excel;
using Microsoft.Win32;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// OUTPUT > Excel 화면 (Milestone 9 §102-113). Idle/Ready/Exporting/Success/Error 상태를
/// IsExporting/IsError/IsSuccess bool 조합으로 표현한다 - ExportWorkflowViewModel(Milestone 5)과
/// 같은 관례를 그대로 따른다. Excel 생성 자체는 IQuantityExcelExportCoordinator에 위임하고, 이
/// ViewModel은 ClosedXML/SQLite를 전혀 모른다.
/// </summary>
public sealed class ExcelExportViewModel : ObservableObject
{
    private readonly IProjectContextService _projectContext;
    private readonly IQuantityExcelExportCoordinator _coordinator;
    private readonly RelayCommand _exportCommand;
    private readonly RelayCommand _openFileCommand;
    private readonly RelayCommand _openFolderCommand;

    private ExcelExportScope _scope = ExcelExportScope.All;
    private bool _includeCalculationBasis = true;
    private bool _includeVerificationDetail = true;
    private bool _includeReviewNotes = true;
    private bool _includeSourceDrawing = true;

    private QuantityWorkbookModel? _preview;
    private bool _isLoadingPreview;
    private bool _isExporting;
    private bool _isSuccess;
    private bool _isError;
    private string? _statusText;
    private string? _lastExportedFile;

    public ExcelExportViewModel(IProjectContextService projectContext, IQuantityExcelExportCoordinator coordinator)
    {
        _projectContext = projectContext;
        _coordinator = coordinator;
        _projectContext.CurrentProjectChanged += async (_, _) => await RefreshPreviewAsync();

        _exportCommand = new RelayCommand(async () => await ExportAsync(), () => CanExport());
        _openFileCommand = new RelayCommand(OpenFile, () => _lastExportedFile is not null);
        _openFolderCommand = new RelayCommand(OpenFolder, () => _lastExportedFile is not null);
    }

    public ICommand ExportCommand => _exportCommand;

    public ICommand OpenFileCommand => _openFileCommand;

    public ICommand OpenFolderCommand => _openFolderCommand;

    public bool HasCurrentProject => _projectContext.CurrentProject is not null;

    public string CurrentProjectName => _projectContext.CurrentProject?.Name ?? string.Empty;

    public bool IsAllScope
    {
        get => _scope == ExcelExportScope.All;
        set { if (value) { Scope = ExcelExportScope.All; } }
    }

    public bool IsVerifiedOnlyScope
    {
        get => _scope == ExcelExportScope.VerifiedOnly;
        set { if (value) { Scope = ExcelExportScope.VerifiedOnly; } }
    }

    public ExcelExportScope Scope
    {
        get => _scope;
        private set
        {
            if (SetProperty(ref _scope, value))
            {
                OnPropertyChanged(nameof(IsAllScope));
                OnPropertyChanged(nameof(IsVerifiedOnlyScope));
                _ = RefreshPreviewAsync();
            }
        }
    }

    public bool IncludeCalculationBasis
    {
        get => _includeCalculationBasis;
        set => SetProperty(ref _includeCalculationBasis, value);
    }

    public bool IncludeVerificationDetail
    {
        get => _includeVerificationDetail;
        set => SetProperty(ref _includeVerificationDetail, value);
    }

    public bool IncludeReviewNotes
    {
        get => _includeReviewNotes;
        set => SetProperty(ref _includeReviewNotes, value);
    }

    public bool IncludeSourceDrawing
    {
        get => _includeSourceDrawing;
        set => SetProperty(ref _includeSourceDrawing, value);
    }

    public bool IsLoadingPreview
    {
        get => _isLoadingPreview;
        private set => SetProperty(ref _isLoadingPreview, value);
    }

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                _exportCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ExportButtonLabel));
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

    /// <summary>"총 42건 · 검토 완료 35 · 확인 필요 5 · 검산 오류 1" - 거대한 KPI 카드 대신 한 줄
    /// 요약(§138, Milestone 7의 History Summary와 같은 원칙).</summary>
    public string SummaryText
    {
        get
        {
            if (!HasCurrentProject)
            {
                return "먼저 프로젝트를 열어주세요.";
            }

            if (_preview is null)
            {
                return IsLoadingPreview ? "불러오는 중..." : "-";
            }

            return _preview.TotalCount == 0
                ? "내보낼 수량이 없습니다."
                : $"총 {_preview.TotalCount}건 · 검토 완료 {_preview.VerifiedCount} · 확인 필요 {_preview.NeedsReviewCount} · 검산 오류 {_preview.VerificationErrorCount}";
        }
    }

    public bool HasRecordsToExport => _preview is { TotalCount: > 0 };

    public string ExportButtonLabel => IsExporting ? "생성 중..." : "Excel 파일 생성";

    public string? LastExportedFileName => _lastExportedFile is null ? null : Path.GetFileName(_lastExportedFile);

    public void OnActivated()
    {
        _ = RefreshPreviewAsync();
    }

    private async System.Threading.Tasks.Task RefreshPreviewAsync()
    {
        OnPropertyChanged(nameof(HasCurrentProject));
        OnPropertyChanged(nameof(CurrentProjectName));
        IsSuccess = false;
        IsError = false;

        if (_projectContext.CurrentProject is not { } project)
        {
            _preview = null;
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(HasRecordsToExport));
            _exportCommand.RaiseCanExecuteChanged();
            return;
        }

        IsLoadingPreview = true;
        OnPropertyChanged(nameof(SummaryText));
        try
        {
            _preview = await _coordinator.BuildPreviewAsync(project.Id, BuildOptions());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Excel export preview failed for project {ProjectId}", project.Id);
            _preview = null;
        }
        finally
        {
            IsLoadingPreview = false;
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(HasRecordsToExport));
            _exportCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanExport() => !_isExporting && HasCurrentProject && HasRecordsToExport;

    private async System.Threading.Tasks.Task ExportAsync()
    {
        if (_projectContext.CurrentProject is not { } project)
        {
            return;
        }

        var suggestedName = ExportFileNameService.Sanitize(project.Name) + "_수량산출서_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx";
        var dialog = new SaveFileDialog
        {
            Title = "수량산출서 Excel로 저장",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = suggestedName,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsExporting = true;
        IsSuccess = false;
        IsError = false;
        StatusText = "Excel 생성 중...";

        try
        {
            var result = await _coordinator.ExportAsync(project.Id, BuildOptions(), dialog.FileName);
            _lastExportedFile = result.FilePath;
            StatusText = $"Excel 저장 완료\n\n{Path.GetFileName(result.FilePath)}";
            IsSuccess = true;
            OnPropertyChanged(nameof(LastExportedFileName));
            _openFileCommand.RaiseCanExecuteChanged();
            _openFolderCommand.RaiseCanExecuteChanged();
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "Excel export failed - file may be locked");
            StatusText = "Excel 파일을 저장하지 못했습니다.\n\n다른 프로그램에서 파일을 사용 중인지 확인해주세요.";
            IsError = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Excel export failed for project {ProjectId}", project.Id);
            StatusText = "Excel 파일을 저장하지 못했습니다.\n\n잠시 후 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsExporting = false;
        }
    }

    private ExcelExportOptions BuildOptions() => new()
    {
        Scope = _scope,
        IncludeCalculationBasis = _includeCalculationBasis,
        IncludeVerificationDetail = _includeVerificationDetail,
        IncludeReviewNotes = _includeReviewNotes,
        IncludeSourceDrawing = _includeSourceDrawing,
    };

    private void OpenFile()
    {
        if (_lastExportedFile is null)
        {
            return;
        }

        // §44: Excel이 설치되어 있지 않아도 파일 생성 자체는 이미 성공했다 - 여는 것만 실패할 수 있고,
        // 그건 Export 실패로 취급하지 않는다.
        try
        {
            Process.Start(new ProcessStartInfo(_lastExportedFile) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open exported Excel file {Path}", _lastExportedFile);
        }
    }

    private void OpenFolder()
    {
        if (_lastExportedFile is null)
        {
            return;
        }

        var folder = Path.GetDirectoryName(_lastExportedFile);
        if (folder is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }
}
