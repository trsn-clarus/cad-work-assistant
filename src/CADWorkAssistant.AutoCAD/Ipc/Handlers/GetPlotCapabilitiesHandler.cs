using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// Milestone 11 §15, §51 - 현재 AutoCAD 환경에서 실제로 가능한 Plot 장치/용지/스타일/Layout을
/// 조회한다 (§16: 장치/용지/스타일 이름을 하드코딩하지 않는다). Read-only.
/// </summary>
internal sealed class GetPlotCapabilitiesHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public GetPlotCapabilitiesHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.GetPlotCapabilities;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var response = await _dispatcher.InvokeAsync(BuildResponseOrNull, cancellationToken).ConfigureAwait(false);

        return response is null
            ? IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."))
            : IpcHandlerResult.Ok(response);
    }

    private static PlotCapabilitiesResponse? BuildResponseOrNull()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return null;
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        var (devices, pdfConfig, pdfDeviceName) = PlotCapabilityReader.ReadDevices();

        var media = pdfConfig is not null && pdfDeviceName is not null
            ? PlotCapabilityReader.ReadMedia(pdfConfig, pdfDeviceName)
            : new List<CadPlotMediaDto>();

        var (colorDependent, named) = PlotCapabilityReader.ReadStyleSheets();
        var currentStyleMode = PlotCapabilityReader.ReadCurrentStyleMode(database);

        using var transaction = database.TransactionManager.StartTransaction();
        var layouts = PlotCapabilityReader.ReadLayouts(database, transaction);

        return new PlotCapabilitiesResponse(devices, media, colorDependent, named, currentStyleMode, layouts);
    }
}
