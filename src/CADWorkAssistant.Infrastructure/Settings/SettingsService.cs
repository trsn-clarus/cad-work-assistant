using System;
using System.IO;
using System.Text.Json;

namespace CADWorkAssistant.Infrastructure.Settings;

/// <summary>
/// %APPDATA%\{appName}\settings.json 에 사용자 설정을 저장/로드한다.
/// 저장은 임시 파일 후 교체 방식으로 처리해 도중에 프로세스가 죽어도 기존 설정이 손상되지 않는다.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;

    public SettingsService(string appName = "CADWorkAssistant")
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appName);
        Directory.CreateDirectory(settingsDirectory);
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // 손상된 설정 파일 때문에 프로그램이 시작조차 못 하면 안 된다 - 기본값으로 대체한다.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        var tempFilePath = _settingsFilePath + ".tmp";
        File.WriteAllText(tempFilePath, json);

        if (File.Exists(_settingsFilePath))
        {
            File.Replace(tempFilePath, _settingsFilePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempFilePath, _settingsFilePath);
        }
    }
}
