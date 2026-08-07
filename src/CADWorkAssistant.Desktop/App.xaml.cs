using System.Windows;
using CADWorkAssistant.Infrastructure.Logging;
using Serilog;

namespace CADWorkAssistant.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppLog.Initialize();
        Log.Information("CAD Work Assistant started");

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("CAD Work Assistant exiting");
        AppLog.Shutdown();
        base.OnExit(e);
    }
}
