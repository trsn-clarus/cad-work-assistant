using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using CADWorkAssistant.Desktop.Common;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Settings/About 화면 (Milestone 8 §159) - 설정값을 바꾸는 화면이 아니라 "이 프로그램이 무엇이고
/// 데이터가 어디 있는지" 확인하는 화면이다. 개발자 스택(IPC/Named Pipe/SQLite 버전/ViewModel 등)은
/// 노출하지 않는다(§96) - 버전, 데이터 위치, 개발사 정도만 보여준다.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private string? _manualErrorMessage;

    public SettingsViewModel(string dataFolderPath, string manualPdfPath)
    {
        DataFolderPath = dataFolderPath;
        ManualPdfPath = manualPdfPath;
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenUserManualCommand = new RelayCommand(OpenUserManual);
    }

    public string AppName => "CAD Work Assistant";

    public string VersionText =>
        Assembly.GetExecutingAssembly().GetName().Version is { } version
            ? $"버전 {version.Major}.{version.Minor}.{version.Build}"
            : "버전 정보 없음";

    public string CompanyText => "Developed by TRSN CLARUS";

    public string DataFolderPath { get; }

    public string ManualPdfPath { get; }

    public ICommand OpenDataFolderCommand { get; }

    public ICommand OpenUserManualCommand { get; }

    /// <summary>사용설명서 PDF를 찾지 못했거나 열지 못했을 때만 채워진다(§60) - 원시 Exception을
    /// 그대로 보여주지 않는다(CLAUDE.md 절대 원칙 4).</summary>
    public string? ManualErrorMessage
    {
        get => _manualErrorMessage;
        private set => SetProperty(ref _manualErrorMessage, value);
    }

    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(DataFolderPath);
            Process.Start(new ProcessStartInfo(DataFolderPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open data folder {Path}", DataFolderPath);
        }
    }

    private void OpenUserManual()
    {
        if (!File.Exists(ManualPdfPath))
        {
            ManualErrorMessage = "사용설명서 파일을 찾을 수 없습니다. 프로그램을 다시 설치하면 해결될 수 있습니다.";
            Log.Warning("User manual PDF not found at {Path}", ManualPdfPath);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(ManualPdfPath) { UseShellExecute = true });
            ManualErrorMessage = null;
        }
        catch (Exception ex)
        {
            ManualErrorMessage = "사용설명서를 여는 중 문제가 발생했습니다. PDF를 열 수 있는 프로그램이 설치되어 있는지 확인해주세요.";
            Log.Error(ex, "Failed to open user manual PDF at {Path}", ManualPdfPath);
        }
    }
}
