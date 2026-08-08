using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Input;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Microsoft.Win32;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// 선택 객체를 새 DWG로 저장한다 (§49-63, §82, §99-103). 파일명 제안/살균은
/// <see cref="ExportFileNameService"/>(Core, AutoCAD 비의존)에 위임하고, 실제 경로 확정과 덮어쓰기
/// 확인은 native SaveFileDialog에 맡긴다(§57 - custom file browser를 만들지 않는다, 덮어쓰기 확인은
/// SaveFileDialog가 기본으로 제공한다). AutoCAD Handler는 "이 Handle들을 이 경로에 WBLOCK으로
/// 저장"만 담당한다.
/// </summary>
public sealed class ExportWorkflowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly RelayCommand _exportCommand;
    private readonly RelayCommand _openFolderCommand;

    private SelectionSession? _session;
    private string _description = string.Empty;
    private bool _isExporting;
    private string? _statusText;
    private bool _isError;
    private string? _lastExportedFolder;

    public ExportWorkflowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _exportCommand = new RelayCommand(Export, CanExport);
        _openFolderCommand = new RelayCommand(OpenFolder, () => _lastExportedFolder is not null);
    }

    public ICommand ExportCommand => _exportCommand;

    public ICommand OpenFolderCommand => _openFolderCommand;

    /// <summary>MainWindowViewModel이 구독해서 Export 이력을 프로젝트에 남긴다 - 소유권을 갖지 않는다
    /// (RecordAdded와 같은 패턴, Milestone 6 §39).</summary>
    public event System.EventHandler<ExportCompletedEventArgs>? ExportCompleted;

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(SuggestedFileName));
            }
        }
    }

    /// <summary>설명을 적는 대로 실시간으로 미리 보여준다 - 저장 대화상자를 열기 전에 이미 뭐가
    /// 저장될지 알 수 있어야 한다(§100).</summary>
    public string? SuggestedFileName => _session?.DrawingName is { } drawingName
        ? ExportFileNameService.SuggestFileName(drawingName, Description)
        : null;

    public bool HasSelection => _session is { ObjectCount: > 0 };

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

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                _exportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    internal void SetSession(SelectionSession? session)
    {
        _session = session;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SuggestedFileName));
        _exportCommand.RaiseCanExecuteChanged();
    }

    private bool CanExport() => !_isExporting && HasSelection;

    private async void Export()
    {
        if (_session is not { ObjectCount: > 0 } session)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "선택 객체를 DWG로 저장",
            Filter = "AutoCAD Drawing (*.dwg)|*.dwg",
            FileName = SuggestedFileName ?? "Export.dwg",
            AddExtension = true,
            OverwritePrompt = true // §55: 덮어쓰기는 native dialog가 확인한다.
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsExporting = true;
        StatusText = "DWG 생성 중...";
        IsError = false;

        try
        {
            var response = await _connectionManager
                .SendRequestAsync(
                    IpcMessageTypes.ExportSelection,
                    new ExportSelectionRequest(session.Handles, dialog.FileName),
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (!response.Success)
            {
                StatusText = response.Error!.Code switch
                {
                    IpcErrorCode.NoActiveDocument => "열려 있는 도면이 없습니다.",
                    _ => "DWG 저장에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요."
                };
                IsError = true;
                return;
            }

            var result = response.DeserializePayload<ExportSelectionResponse>();
            _lastExportedFolder = Path.GetDirectoryName(dialog.FileName);
            _openFolderCommand.RaiseCanExecuteChanged();

            var objectCount = result?.ObjectCount ?? session.ObjectCount;
            StatusText = $"저장 완료 - {Path.GetFileName(dialog.FileName)} ({objectCount}개 객체)";
            ExportCompleted?.Invoke(this, new ExportCompletedEventArgs(session.DrawingName, dialog.FileName, objectCount, Description));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ExportSelection failed");
            StatusText = "DWG 저장에 실패했습니다.\n\nAutoCAD 연결 상태를 확인한 뒤 다시 시도해주세요.";
            IsError = true;
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void OpenFolder()
    {
        if (_lastExportedFolder is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = _lastExportedFolder, UseShellExecute = true });
    }
}

public sealed record ExportCompletedEventArgs(string? SourceDrawing, string TargetFile, int ObjectCount, string? Description);
