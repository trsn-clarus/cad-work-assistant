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
    public SettingsViewModel(string dataFolderPath)
    {
        DataFolderPath = dataFolderPath;
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
    }

    public string AppName => "CAD Work Assistant";

    public string VersionText =>
        Assembly.GetExecutingAssembly().GetName().Version is { } version
            ? $"버전 {version.Major}.{version.Minor}.{version.Build}"
            : "버전 정보 없음";

    public string CompanyText => "Developed by TRSN CLARUS";

    public string DataFolderPath { get; }

    public ICommand OpenDataFolderCommand { get; }

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
}
