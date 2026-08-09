using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>
/// Milestone 11 §97-98 - 실제 AutoCAD Plot 엔진을 흉내내지 않는다. Milestone 10의
/// QuantityPdfBuilder(수량 보고서 PDF)를 재사용하지 않는다 - 이건 완전히 다른 서브시스템이다(§98,
/// 명시적으로 금지됨). FakeExportSelectionHandler가 가짜 DWG 대신 평문 안내 텍스트 파일을 쓰는
/// 것과 같은 방식으로, 대상 경로에 "이것은 진짜 Plot 출력이 아니다"라는 문구만 남긴다 - 진짜 PDF
/// 바이너리를 흉내내려 하지 않는다(그렇게 하면 실수로 "진짜처럼 보이는" 결과를 만들 위험이 있다).
/// </summary>
internal sealed class FakePlotDrawingPdfHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakePlotDrawingPdfHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.PlotDrawingPdf;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<PlotDrawingPdfRequest>(IpcJson.Options);
        if (request is null || string.IsNullOrWhiteSpace(request.TargetFilePath))
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "PlotDrawingPdf requires a target file path."));
        }

        switch (_scenario.PlotDrawingPdfBehavior)
        {
            case PlotDrawingBehavior.Busy:
                return IpcHandlerResult.Fail(new IpcError(
                    IpcErrorCode.ApiExecutionFailed, "AutoCAD is currently plotting another document. Please wait and try again."));

            case PlotDrawingBehavior.Failure:
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, "Simulated AutoCAD plot failure."));

            case PlotDrawingBehavior.DisconnectBeforeResponding:
                Environment.Exit(0);
                return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InternalError, "unreachable"));

            case PlotDrawingBehavior.Succeed:
            default:
                return await SucceedAsync(request).ConfigureAwait(false);
        }
    }

    private async Task<IpcHandlerResult> SucceedAsync(PlotDrawingPdfRequest request)
    {
        try
        {
            await File.WriteAllTextAsync(
                request.TargetFilePath,
                "CADWorkAssistant FakeAutoCad placeholder plot - not a real AutoCAD Plot output.").ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, ex.Message));
        }

        var device = PlotPdfDeviceSelector.SelectBest(_scenario.PlotDevices);
        var media = PlotPaperMatcher.FindMatch(
            _scenario.PlotMedia,
            request.PaperSizeName == CadPaperSizeCatalog.A3.Name ? CadPaperSizeCatalog.A3 : CadPaperSizeCatalog.A4);
        var styleResolution = PlotStyleResolver.Resolve(
            request.ColorMode, _scenario.PlotCurrentStyleMode, _scenario.PlotColorDependentStyleSheets, _scenario.PlotNamedStyleSheets);
        var resolvedOrientation = PlotOrientationResolver.Resolve(request.Orientation, request.Window);

        var response = new PlotDrawingPdfResponse(
            request.TargetFilePath,
            device?.Name ?? "(simulated device)",
            media?.CanonicalName ?? "(simulated media)",
            media?.LocaleName ?? request.PaperSizeName,
            styleResolution.StyleSheetName ?? "(layout default)",
            media?.WidthMm ?? 0,
            media?.HeightMm ?? 0,
            resolvedOrientation,
            elapsedMs: 42,
            warning: "This is FakeAutoCad simulation output, not a real AutoCAD plot.");

        return IpcHandlerResult.Ok(response);
    }
}
