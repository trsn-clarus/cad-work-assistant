using System;
using System.IO;
using Serilog;

namespace CADWorkAssistant.Infrastructure.Logging;

/// <summary>
/// Desktop과 AutoCAD Plugin이 공유하는 Serilog 부트스트래퍼.
/// 초기화 후에는 <see cref="Serilog.Log"/>를 그대로 사용한다.
/// </summary>
public static class AppLog
{
    private static bool _initialized;

    public static string Initialize(string appName = "CADWorkAssistant")
    {
        if (_initialized)
        {
            return LogDirectory(appName);
        }

        var logDirectory = LogDirectory(appName);
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _initialized = true;
        return logDirectory;
    }

    public static void Shutdown()
    {
        Log.CloseAndFlush();
        _initialized = false;
    }

    private static string LogDirectory(string appName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        appName,
        "logs");
}
