using System.Windows;
using CADWorkAssistant.Desktop.Services;
using CADWorkAssistant.Desktop.ViewModels;
using CADWorkAssistant.Infrastructure.Logging;
using CADWorkAssistant.Persistence;
using Serilog;

namespace CADWorkAssistant.Desktop;

public partial class App : Application
{
    private IAutoCadConnectionManager? _connectionManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppLog.Initialize();
        Log.Information("CAD Work Assistant started");

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
        };

        // 간단한 수동 composition root - 이 규모 앱에 DI 컨테이너는 과하다 (§39 과도한 프레임워크 지양).
        // ConnectionManager는 반드시 UI 스레드(OnStartup)에서 생성해야 WPF의 DispatcherSynchronizationContext를
        // 캡처해서 PropertyChanged를 UI 스레드로 marshal할 수 있다.
        var discoveryService = new AutoCadDiscoveryService();
        _connectionManager = new AutoCadConnectionManager(discoveryService);

        // DB 경로 결정(CWA_DATABASE_PATH override/Simulation Mode 분리)과 마이그레이션은
        // CadWorkAssistantDatabase.OpenConnection()이 처음 호출될 때(ProjectContextService.
        // InitializeAsync) 알아서 처리한다.
        var database = new CadWorkAssistantDatabase();
        var projectDataService = new ProjectDataService(database);
        var projectContext = new ProjectContextService(projectDataService);
        var verificationCoordinator = new QuantityVerificationCoordinator(projectDataService);

        var mainWindowViewModel = new MainWindowViewModel(_connectionManager, projectContext, verificationCoordinator);
        var mainWindow = new MainWindow(mainWindowViewModel, projectContext);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("CAD Work Assistant exiting");
        _connectionManager?.Dispose();
        AppLog.Shutdown();
        base.OnExit(e);
    }
}
