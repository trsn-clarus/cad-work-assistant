using Autodesk.AutoCAD.Runtime;
using CADWorkAssistant.Infrastructure.Logging;
using Serilog;

[assembly: ExtensionApplication(typeof(CADWorkAssistant.AutoCAD.Extension))]

namespace CADWorkAssistant.AutoCAD;

/// <summary>
/// AutoCAD 프로세스에 로드되는 진입점. 지금은 로딩/언로딩만 검증한다.
/// 실제 명령(CWA_*)과 Named Pipe 서버는 Milestone 1에서 추가한다 (docs/ROADMAP.md).
/// </summary>
public class Extension : IExtensionApplication
{
    public void Initialize()
    {
        AppLog.Initialize();
        Log.Information("CADWorkAssistant.AutoCAD plugin loaded");
    }

    public void Terminate()
    {
        Log.Information("CADWorkAssistant.AutoCAD plugin unloading");
    }
}
