using System.IO;
using System.Threading;
using System.Windows;
using CADWorkAssistant.Desktop.Services;
using CADWorkAssistant.Desktop.ViewModels;
using CADWorkAssistant.Infrastructure.Logging;
using CADWorkAssistant.Persistence;
using Serilog;

namespace CADWorkAssistant.Desktop;

public partial class App : Application
{
    // installer/CADWorkAssistant.iss의 AppMutex와 반드시 같은 이름이어야 한다 - Setup/Uninstall이
    // 실행 중인 인스턴스를 감지해 먼저 종료를 안내할 수 있다(Milestone 8 §146). 동시에 같은 SQLite
    // 파일에 두 인스턴스가 쓰는 것도 막아준다 - 원래 없던 문제였지만 손해볼 게 없는 안전장치다.
    private const string SingleInstanceMutexName = "Global\\CADWorkAssistant.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private IAutoCadConnectionManager? _connectionManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "CAD Work Assistant가 이미 실행 중입니다.",
                "CAD Work Assistant",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

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
        var excelExportCoordinator = new QuantityExcelExportCoordinator(projectDataService, verificationCoordinator, projectContext);

        // Settings 화면의 "데이터 폴더 열기"용 - DB 파일이 아니라 그 상위(data/의 부모) 폴더를 보여준다.
        // Simulation/Real이 서로 다른 db 파일을 쓰지만(CadWorkAssistantDatabase) 상위 폴더는 같다.
        var dataFolderPath = Path.GetDirectoryName(Path.GetDirectoryName(database.DatabasePath)) ?? database.DatabasePath;

        var mainWindowViewModel = new MainWindowViewModel(_connectionManager, projectContext, verificationCoordinator, excelExportCoordinator, dataFolderPath);
        var mainWindow = new MainWindow(mainWindowViewModel, projectContext);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("CAD Work Assistant exiting");
        _connectionManager?.Dispose();
        AppLog.Shutdown();
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
