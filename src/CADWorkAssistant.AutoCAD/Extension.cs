using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;
using CADWorkAssistant.AutoCAD.Ipc;
using CADWorkAssistant.AutoCAD.Ipc.Handlers;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Infrastructure.Logging;
using Serilog;

[assembly: ExtensionApplication(typeof(CADWorkAssistant.AutoCAD.Extension))]

namespace CADWorkAssistant.AutoCAD;

/// <summary>
/// AutoCAD 프로세스에 로드되는 진입점. Named Pipe IPC 서버를 시작/종료한다 (docs/AUTOCAD_INTEGRATION.md §5).
/// </summary>
public class Extension : IExtensionApplication
{
    private AutoCadPipeServer? _pipeServer;

    public void Initialize()
    {
        AppLog.Initialize();
        Log.Information("CADWorkAssistant.AutoCAD plugin loaded");

        var dispatcher = new AutoCadDispatcher();
        var handlers = new IIpcRequestHandler[]
        {
            new PingHandler(),
            new GetApplicationInfoHandler(dispatcher),
            new GetDrawingContextHandler(dispatcher),
            new SelectLengthObjectsHandler(dispatcher)
        };

        _pipeServer = new AutoCadPipeServer(new IpcRequestDispatcher(handlers), Process.GetCurrentProcess().Id);
        _pipeServer.Start();
    }

    public void Terminate()
    {
        Log.Information("CADWorkAssistant.AutoCAD plugin unloading");

        // AutoCAD 종료를 막지 않도록 짧게 기다린 뒤 포기한다 - 파이프 서버가 이미 멈춰 있어도 안전하다.
        _pipeServer?.StopAsync().Wait(millisecondsTimeout: 2000);

        AppLog.Shutdown();
    }
}
