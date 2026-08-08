using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

internal sealed class GetApplicationInfoHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public GetApplicationInfoHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.GetApplicationInfo;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var version = await _dispatcher.InvokeAsync(() => Application.Version, cancellationToken).ConfigureAwait(false);
        var processId = Process.GetCurrentProcess().Id;

        var info = new AutoCadInstanceInfo(
            AutoCadVersionMap.ToProductName(version),
            version.ToString(),
            processId,
            PluginInfo.Version,
            IpcProtocol.CurrentVersion);

        return IpcHandlerResult.Ok(info);
    }
}
