using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Core.Plot;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Microsoft.Win32;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// OUTPUT > 도면 PDF 화면 (Milestone 11 §68-79, §139). Milestone 10의 PdfExportViewModel(수량
/// 보고서 PDF, Persistence 기반)과 완전히 다른 화면이다 - 이 화면은 실제 AutoCAD Plot 엔진을
/// 호출한다. 상태 흐름: Disconnected -> CapabilitiesLoading -> (NoPdfDevice | Ready) ->
/// (WindowAwaiting, Window scope만) -> Plotting -> Success/Error.
/// </summary>
public sealed class DrawingPdfExportViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly IPlotCapabilityCoordinator _capabilityCoordinator;
    private readonly IDrawingPdfExportCoordinator _exportCoordinator;

    private readonly RelayCommand _selectWindowCommand;
    private readonly RelayCommand _exportCommand;
    private readonly RelayCommand _openFileCommand;
    private readonly RelayCommand _openFolderCommand;

    private bool _isLoadingCapabilities;
    private PlotCapabilitiesResponse? _capabilities;
    private string? _loadErrorText;

    private CadPlotScope _scope = CadPlotScope.Window;
    private string? _selectedLayoutName;
    private CadPaperSize _paperSize = CadPaperSizeCatalog.A4;
    private CadPlotOrientation _orientation = CadPlotOrientation.Auto;
    private CadPlotColorMode _colorMode = CadPlotColorMode.KeepExisting;

    private CadPlotWindowDto? _window;
    private bool _isSelectingWindow;

    private bool _isExporting;
    private bool _isSuccess;
    private bool _isError;
    private string? _statusText;
    private string? _lastExportedFile;

    public DrawingPdfExportViewModel(
        IAutoCadConnectionManager connectionManager,
        IPlotCapabilityCoordinator capabilityCoordinator,
        IDrawingPdfExportCoordinator exportCoordinator)
    {
        _connectionManager = connectionManager;
        _capabilityCoordinator = capabilityCoordinator;
        _exportCoordinator = exportCoordinator;

        _connectionManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(IAutoCadConnectionManager.State))
            {
                return;
            }

            OnPropertyChanged(nameof(IsConnected));
            RaiseAllCanExecuteChanged();
            if (IsConnected && _capabilities is null)
            {
                _ = LoadCapabilitiesAsync();
            }
        };

        _selectWindowCommand = new RelayCommand(async () => await SelectWindowAsync(), CanSelectWindow);
        _exportCommand = new RelayCommand(async () => await ExportAsync(), CanExport);
        _openFileCommand = new RelayCommand(OpenFile, () => _lastExportedFile is not null);
        _openFolderCommand = new RelayCommand(OpenFolder, () => _lastExportedFile is not null);
    }

    public ICommand SelectWindowCommand => _selectWindowCommand;
    public ICommand ExportCommand => _exportCommand;
    public ICommand OpenFileCommand => _openFileCommand;
    public ICommand OpenFolderCommand => _openFolderCommand;

    public bool IsConnected => _connectionManager.State == CadConnectionState.Connected;

    public bool IsLoadingCapabilities
    {
        get => _isLoadingCapabilities;
        private set => SetProperty(ref _isLoadingCapabilities, value);
    }

    public bool HasCapabilities => _capabilities is not null;

    public bool HasPdfDevice => _capabilities?.Devices.Any(d => d.IsPdfCapable) == true;

    public string? LoadErrorText
    {
        get => _loadErrorText;
        private set => SetProperty(ref _loadErrorText, value);
    }

    public bool ShowLoadErrorMessage => !IsLoadingCapabilities && LoadErrorText is not null;

    public bool ShowNoPdfDeviceMessage => !IsLoadingCapabilities && HasCapabilities && !HasPdfDevice;

    public bool ShowReadyForm => !IsLoadingCapabilities && HasCapabilities && HasPdfDevice;

    public bool IsCurrentLayoutScope
    {
        get => _scope == CadPlotScope.CurrentLayout;
        set { if (value) { Scope = CadPlotScope.CurrentLayout; } }
    }

    public bool IsWindowScope
    {
        get => _scope == CadPlotScope.Window;
        set { if (value) { Scope = CadPlotScope.Window; } }
    }

    public CadPlotScope Scope
    {
        get => _scope;
        private set
        {
            if (SetProperty(ref _scope, value))
            {
                OnPropertyChanged(nameof(IsCurrentLayoutScope));
                OnPropertyChanged(nameof(IsWindowScope));
                RaiseAllCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<CadPlotLayoutDto> Layouts =>
        _capabilities?.Layouts.Where(l => !l.IsModel).ToList() ?? new List<CadPlotLayoutDto>();

    public string? SelectedLayoutName
    {
        get => _selectedLayoutName;
        set => SetProperty(ref _selectedLayoutName, value);
    }

    public bool IsA4
    {
        get => _paperSize.Name == CadPaperSizeCatalog.A4.Name;
        set { if (value) { PaperSize = CadPaperSizeCatalog.A4; } }
    }

    public bool IsA3
    {
        get => _paperSize.Name == CadPaperSizeCatalog.A3.Name;
        set { if (value) { PaperSize = CadPaperSizeCatalog.A3; } }
    }

    public bool IsA4Available => _capabilities is not null && PlotPaperMatcher.FindMatch(_capabilities.Media, CadPaperSizeCatalog.A4) is not null;

    public bool IsA3Available => _capabilities is not null && PlotPaperMatcher.FindMatch(_capabilities.Media, CadPaperSizeCatalog.A3) is not null;

    public CadPaperSize PaperSize
    {
        get => _paperSize;
        private set
        {
            if (_paperSize.Name == value.Name)
            {
                return;
            }

            _paperSize = value;
            OnPropertyChanged(nameof(PaperSize));
            OnPropertyChanged(nameof(IsA4));
            OnPropertyChanged(nameof(IsA3));
        }
    }

    public bool IsAutoOrientation
    {
        get => _orientation == CadPlotOrientation.Auto;
        set { if (value) { Orientation = CadPlotOrientation.Auto; } }
    }

    public bool IsPortraitOrientation
    {
        get => _orientation == CadPlotOrientation.Portrait;
        set { if (value) { Orientation = CadPlotOrientation.Portrait; } }
    }

    public bool IsLandscapeOrientation
    {
        get => _orientation == CadPlotOrientation.Landscape;
        set { if (value) { Orientation = CadPlotOrientation.Landscape; } }
    }

    public CadPlotOrientation Orientation
    {
        get => _orientation;
        private set
        {
            if (SetProperty(ref _orientation, value))
            {
                OnPropertyChanged(nameof(IsAutoOrientation));
                OnPropertyChanged(nameof(IsPortraitOrientation));
                OnPropertyChanged(nameof(IsLandscapeOrientation));
            }
        }
    }

    public bool IsKeepExistingColor
    {
        get => _colorMode == CadPlotColorMode.KeepExisting;
        set { if (value) { ColorMode = CadPlotColorMode.KeepExisting; } }
    }

    public bool IsMonochromeColor
    {
        get => _colorMode == CadPlotColorMode.Monochrome;
        set { if (value) { ColorMode = CadPlotColorMode.Monochrome; } }
    }

    /// <summary>§33 - STB 도면이거나 monochrome.ctb가 없으면 비활성화된다. 실제 판정은 항상
    /// PlotStyleResolver(Core)가 한다 - 여기서 다시 규칙을 만들지 않는다.</summary>
    public bool IsMonochromeAvailable => _capabilities is not null && PlotStyleResolver.Resolve(
        CadPlotColorMode.Monochrome,
        _capabilities.CurrentDrawingStyleMode,
        _capabilities.ColorDependentStyleSheets,
        _capabilities.NamedStyleSheets).IsAvailable;

    public CadPlotColorMode ColorMode
    {
        get => _colorMode;
        private set
        {
            if (SetProperty(ref _colorMode, value))
            {
                OnPropertyChanged(nameof(IsKeepExistingColor));
                OnPropertyChanged(nameof(IsMonochromeColor));
            }
        }
    }

    public bool HasWindow => _window is not null;

    public string WindowSummaryText => _window is { } w
        ? $"{w.Width:N0} × {w.Height:N0} (도면 단위)"
        : "아직 영역을 지정하지 않았습니다.";

    public bool IsSelectingWindow
    {
        get => _isSelectingWindow;
        private set
        {
            if (SetProperty(ref _isSelectingWindow, value))
            {
                OnPropertyChanged(nameof(SelectWindowButtonLabel));
            }
        }
    }

    public string SelectWindowButtonLabel => IsSelectingWindow ? "지정 중..." : "영역 지정";

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
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

    public string ExportButtonLabel => IsExporting ? "출력 중..." : "PDF로 저장";

    public string? LastExportedFileName => _lastExportedFile is null ? null : Path.GetFileName(_lastExportedFile);

    public void OnActivated()
    {
        if (IsConnected)
        {
            _ = LoadCapabilitiesAsync();
        }
    }

    private async System.Threading.Tasks.Task LoadCapabilitiesAsync()
    {
        IsLoadingCapabilities = true;
        LoadErrorText = null;
        IsSuccess = false;
        IsError = false;
        OnPropertyChanged(nameof(HasCapabilities));
        OnPropertyChanged(nameof(ShowLoadErrorMessage));
        OnPropertyChanged(nameof(ShowNoPdfDeviceMessage));
        OnPropertyChanged(nameof(ShowReadyForm));
        RaiseAllCanExecuteChanged();

        var result = await _capabilityCoordinator.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        if (result.Success)
        {
            _capabilities = result.Capabilities;

            var nonModelLayouts = _capabilities!.Layouts.Where(l => !l.IsModel).ToList();
            _selectedLayoutName = nonModelLayouts.FirstOrDefault(l => l.IsCurrent)?.Name ?? nonModelLayouts.FirstOrDefault()?.Name;
            OnPropertyChanged(nameof(SelectedLayoutName));
            OnPropertyChanged(nameof(Layouts));

            // §22: 선택한 용지가 이 장치에서 지원되지 않으면 지원되는 쪽으로 자동 전환한다.
            if (PaperSize.Name == CadPaperSizeCatalog.A4.Name && !IsA4Available && IsA3Available)
            {
                PaperSize = CadPaperSizeCatalog.A3;
            }
            else if (PaperSize.Name == CadPaperSizeCatalog.A3.Name && !IsA3Available && IsA4Available)
            {
                PaperSize = CadPaperSizeCatalog.A4;
            }

            if (ColorMode == CadPlotColorMode.Monochrome && !IsMonochromeAvailable)
            {
                ColorMode = CadPlotColorMode.KeepExisting;
            }
        }
        else
        {
            _capabilities = null;
            LoadErrorText = DescribeError(result.Error!, "Plot 기능 정보를 불러오지 못했습니다.");
        }

        IsLoadingCapabilities = false;
        OnPropertyChanged(nameof(HasCapabilities));
        OnPropertyChanged(nameof(HasPdfDevice));
        OnPropertyChanged(nameof(IsA4Available));
        OnPropertyChanged(nameof(IsA3Available));
        OnPropertyChanged(nameof(IsMonochromeAvailable));
        OnPropertyChanged(nameof(Layouts));
        OnPropertyChanged(nameof(ShowLoadErrorMessage));
        OnPropertyChanged(nameof(ShowNoPdfDeviceMessage));
        OnPropertyChanged(nameof(ShowReadyForm));
        RaiseAllCanExecuteChanged();
    }

    private bool CanSelectWindow() =>
        IsConnected && HasCapabilities && HasPdfDevice && IsWindowScope && !_isSelectingWindow && !_isExporting;

    private async System.Threading.Tasks.Task SelectWindowAsync()
    {
        IsSelectingWindow = true;
        StatusText = "AutoCAD에서 영역을 지정해주세요...";
        IsError = false;
        RaiseAllCanExecuteChanged();

        try
        {
            var outcome = await PlotWindowSelector.SelectAsync(_connectionManager, CancellationToken.None).ConfigureAwait(true);
            switch (outcome.Kind)
            {
                case PlotWindowOutcomeKind.Selected:
                    _window = outcome.Window;
                    StatusText = "영역을 지정했습니다.";
                    break;

                case PlotWindowOutcomeKind.Cancelled:
                    StatusText = "영역 지정이 취소되었습니다.";
                    break;

                default:
                    StatusText = DescribeError(outcome.Error!, "영역 지정에 실패했습니다.");
                    IsError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AcquirePlotWindow failed");
            StatusText = "영역 지정에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsSelectingWindow = false;
            OnPropertyChanged(nameof(HasWindow));
            OnPropertyChanged(nameof(WindowSummaryText));
            RaiseAllCanExecuteChanged();
        }
    }

    private bool CanExport() =>
        IsConnected && HasCapabilities && HasPdfDevice && !_isExporting && !_isSelectingWindow
        && (Scope == CadPlotScope.CurrentLayout || HasWindow);

    private async System.Threading.Tasks.Task ExportAsync()
    {
        var drawingName = _connectionManager.Drawing?.DocumentDisplayName ?? "Drawing.dwg";
        var suggestedName = PlotOutputFileNameService.SuggestFileName(
            drawingName,
            Scope,
            Scope == CadPlotScope.CurrentLayout ? SelectedLayoutName : null,
            PaperSize,
            ColorMode,
            DateTime.Now);

        var dialog = new SaveFileDialog
        {
            Title = "도면 PDF로 저장",
            Filter = "PDF Document (*.pdf)|*.pdf",
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
        StatusText = "PDF 출력 중...";
        RaiseAllCanExecuteChanged();

        try
        {
            var request = new PlotDrawingPdfRequest(
                Scope,
                Scope == CadPlotScope.CurrentLayout ? SelectedLayoutName : null,
                Scope == CadPlotScope.Window ? _window : null,
                PaperSize.Name,
                Orientation,
                ColorMode,
                dialog.FileName);

            var outcome = await _exportCoordinator.ExportAsync(request, CancellationToken.None).ConfigureAwait(true);
            if (outcome.Success)
            {
                var response = outcome.Response!;
                _lastExportedFile = response.OutputFile;
                StatusText = response.Warning is { } warning
                    ? $"PDF 저장 완료\n\n{Path.GetFileName(response.OutputFile)}\n\n{warning}"
                    : $"PDF 저장 완료\n\n{Path.GetFileName(response.OutputFile)}";
                IsSuccess = true;
                OnPropertyChanged(nameof(LastExportedFileName));
                _openFileCommand.RaiseCanExecuteChanged();
                _openFolderCommand.RaiseCanExecuteChanged();
            }
            else
            {
                StatusText = DescribeError(outcome.Error!, "도면 PDF 저장에 실패했습니다.");
                IsError = true;
            }
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "PlotDrawingPdf failed - file may be locked");
            StatusText = "PDF 파일을 저장하지 못했습니다.\n\n다른 프로그램에서 파일을 사용 중인지 확인해주세요.";
            IsError = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PlotDrawingPdf failed");
            StatusText = "도면 PDF 저장에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsExporting = false;
            RaiseAllCanExecuteChanged();
        }
    }

    private void OpenFile()
    {
        if (_lastExportedFile is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_lastExportedFile) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open exported drawing PDF {Path}", _lastExportedFile);
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

    private static string DescribeError(IpcError error, string genericMessage) => error.Code switch
    {
        IpcErrorCode.NoActiveDocument => "열려 있는 도면이 없습니다.",
        IpcErrorCode.Timeout => "AutoCAD 응답이 지연되고 있습니다.\n\n잠시 후 다시 시도해주세요.",
        _ => genericMessage + "\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요."
    };

    private void RaiseAllCanExecuteChanged()
    {
        _selectWindowCommand.RaiseCanExecuteChanged();
        _exportCommand.RaiseCanExecuteChanged();
    }
}
