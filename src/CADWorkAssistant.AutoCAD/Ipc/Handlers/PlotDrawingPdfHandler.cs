using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// Milestone 11 §38-50 - 실제 AutoCAD Plot 엔진으로 DWG를 PDF로 출력한다. Layout의 PlotSettings를
/// 영구 변경하지 않는다(§6 원본 비변경 원칙) - CopyFrom으로 뜬 임시 PlotSettings만 override로 쓰고
/// 끝나면 그대로 버린다. 모든 타입/메서드/열거값은 실제 설치된 AutoCAD 2024 accoremgd.dll/acdbmgd.dll을
/// 리플렉션으로 확인한 뒤 사용했다(§4-5) - PlotFactory에는 CreatePlotEngine()이 없고
/// CreatePublishEngine()만 있다는 것도 이렇게 확인했다. Non-interactive라 InvokeAsync로 실행한다
/// (Plot Window는 이미 AcquirePlotWindow로 앞서 받아온 좌표를 쓴다).
/// </summary>
internal sealed class PlotDrawingPdfHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public PlotDrawingPdfHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.PlotDrawingPdf;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<PlotDrawingPdfRequest>(IpcJson.Options);
        if (request is null || string.IsNullOrWhiteSpace(request.TargetFilePath))
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "PlotDrawingPdf requires a target file path."));
        }

        if (request.Scope == CadPlotScope.Window && request.Window is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "Window scope requires a plot window."));
        }

        var targetPaperSize = CadPaperSizeCatalog.BuiltIn.FirstOrDefault(
            size => string.Equals(size.Name, request.PaperSizeName, StringComparison.OrdinalIgnoreCase));
        if (targetPaperSize is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, $"Unknown paper size: {request.PaperSizeName}."));
        }

        var outcome = await _dispatcher.InvokeAsync(() => Plot(request, targetPaperSize), cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            { Success: true } => IpcHandlerResult.Ok(outcome.Response),
            { NoActiveDocument: true } => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            { IsInvalidRequest: true } => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.InvalidRequest, outcome.ErrorMessage ?? "Invalid plot request.")),
            _ => IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, outcome.ErrorMessage ?? "Plot failed."))
        };
    }

    private static PlotOutcome Plot(PlotDrawingPdfRequest request, CadPaperSize targetPaperSize)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return PlotOutcome.NoDocument();
        }

        // §60-61: 이미 다른 Plot이 진행 중이면 시작하지 않는다 - 여러 Plot 요청이 겹치면 AutoCAD
        // Plot 엔진 자체가 지원하지 않는다.
        if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
        {
            return PlotOutcome.Failed("AutoCAD is currently plotting another document. Please wait and try again.");
        }

        var directory = Path.GetDirectoryName(request.TargetFilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return PlotOutcome.Failed($"Target folder does not exist: {directory}");
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        // §14, §6: Layout(DBObject)은 Transaction이 열려 있는 동안에만 안전하게 읽을 수 있다 - 닫힌
        // Transaction의 객체를 나중에 다시 쓰면 런타임에서 깨진다. 그래서 CopyFrom까지 이 Transaction
        // 범위 안에서 끝내고, PlotSettings(별도 detached 객체, CopyFrom이 값만 복사해온다)만 밖으로
        // 들고 나온다.
        ObjectId layoutId;
        PlotSettings overrideSettings;

        using (var transaction = database.TransactionManager.StartTransaction())
        {
            if (!PlotCapabilityReader.TryResolveLayout(database, transaction, request.Scope, request.LayoutName, out layoutId, out var layout))
            {
                return PlotOutcome.InvalidRequestFailure("The requested layout was not found.");
            }

            overrideSettings = new PlotSettings(layout.ModelType);
            overrideSettings.CopyFrom(layout);

            transaction.Commit(); // Read-only 조회였다 - 원본을 바꾸지 않았다.
        }

        using var overrideSettingsScope = overrideSettings;

        var (devices, pdfConfig, pdfDeviceName) = PlotCapabilityReader.ReadDevices();
        _ = devices;
        if (pdfConfig is null || pdfDeviceName is null)
        {
            return PlotOutcome.Failed("No PDF-capable plot device was found.");
        }

        var media = PlotCapabilityReader.ReadMedia(pdfConfig, pdfDeviceName);
        var matchedMedia = PlotPaperMatcher.FindMatch(media, targetPaperSize);
        if (matchedMedia is null)
        {
            return PlotOutcome.Failed($"The device '{pdfDeviceName}' does not support {targetPaperSize.Name} paper.");
        }

        var (colorDependentStyles, namedStyles) = PlotCapabilityReader.ReadStyleSheets();
        var currentStyleMode = PlotCapabilityReader.ReadCurrentStyleMode(database);
        var styleResolution = PlotStyleResolver.Resolve(request.ColorMode, currentStyleMode, colorDependentStyles, namedStyles);
        if (!styleResolution.IsAvailable)
        {
            return PlotOutcome.Failed(styleResolution.UnavailableReason ?? "The requested plot style is not available.");
        }

        var resolvedOrientation = PlotOrientationResolver.Resolve(request.Orientation, request.Window);

        var stopwatch = Stopwatch.StartNew();
        var tempFilePath = Path.Combine(directory, $".plot_{Guid.NewGuid():N}.tmp");

        try
        {
            var validator = PlotSettingsValidator.Current;
            validator.SetPlotConfigurationName(overrideSettings, pdfDeviceName, null);
            validator.RefreshLists(overrideSettings);
            validator.SetCanonicalMediaName(overrideSettings, matchedMedia.CanonicalName);
            validator.SetPlotRotation(
                overrideSettings,
                resolvedOrientation == CadPlotOrientation.Landscape ? PlotRotation.Degrees090 : PlotRotation.Degrees000);
            validator.SetPlotType(
                overrideSettings,
                request.Scope == CadPlotScope.Window
                    ? Autodesk.AutoCAD.DatabaseServices.PlotType.Window
                    : Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);

            if (request.Scope == CadPlotScope.Window && request.Window is not null)
            {
                validator.SetPlotWindowArea(
                    overrideSettings,
                    new Extents2d(request.Window.MinX, request.Window.MinY, request.Window.MaxX, request.Window.MaxY));
                validator.SetPlotCentered(overrideSettings, true);
            }

            validator.SetUseStandardScale(overrideSettings, true);
            validator.SetStdScaleType(overrideSettings, StdScaleType.ScaleToFit);

            if (styleResolution.StyleSheetName is not null)
            {
                validator.SetCurrentStyleSheet(overrideSettings, styleResolution.StyleSheetName);
            }

            using var plotInfo = new PlotInfo { Layout = layoutId, OverrideSettings = overrideSettings };
            new PlotInfoValidator().Validate(plotInfo);

            using (var plotEngine = PlotFactory.CreatePublishEngine())
            {
                plotEngine.BeginPlot(null, null);
                plotEngine.BeginDocument(plotInfo, document.Name, null, 1, plotToFile: true, tempFilePath);

                var pageInfo = new PlotPageInfo();
                plotEngine.BeginPage(pageInfo, plotInfo, lastPage: true, null);
                plotEngine.BeginGenerateGraphics(null);
                plotEngine.EndGenerateGraphics(null);
                plotEngine.EndPage(null);

                plotEngine.EndDocument(null);
                plotEngine.EndPlot(null);
            }

            if (File.Exists(request.TargetFilePath))
            {
                File.Delete(request.TargetFilePath);
            }

            File.Move(tempFilePath, request.TargetFilePath);
            stopwatch.Stop();

            var response = new PlotDrawingPdfResponse(
                request.TargetFilePath,
                pdfDeviceName,
                matchedMedia.CanonicalName,
                matchedMedia.LocaleName,
                styleResolution.StyleSheetName ?? "(layout default)",
                targetPaperSize.PortraitWidthMm,
                targetPaperSize.PortraitHeightMm,
                resolvedOrientation,
                stopwatch.ElapsedMilliseconds,
                warning: null);

            return PlotOutcome.Succeeded(response);
        }
        catch (Exception ex) when (ex is Autodesk.AutoCAD.Runtime.Exception or IOException or UnauthorizedAccessException)
        {
            return PlotOutcome.Failed(ex.Message);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (IOException)
                {
                    // 임시 파일 정리 실패는 Plot 결과 자체를 실패로 만들지 않는다.
                }
            }
        }
    }

    private sealed class PlotOutcome
    {
        private PlotOutcome(bool success, bool noActiveDocument, bool isInvalidRequest, PlotDrawingPdfResponse? response, string? errorMessage)
        {
            Success = success;
            NoActiveDocument = noActiveDocument;
            IsInvalidRequest = isInvalidRequest;
            Response = response;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public bool NoActiveDocument { get; }
        public bool IsInvalidRequest { get; }
        public PlotDrawingPdfResponse? Response { get; }
        public string? ErrorMessage { get; }

        public static PlotOutcome Succeeded(PlotDrawingPdfResponse response) => new(true, false, false, response, null);
        public static PlotOutcome Failed(string message) => new(false, false, false, null, message);
        public static PlotOutcome InvalidRequestFailure(string message) => new(false, false, true, null, message);
        public static PlotOutcome NoDocument() => new(false, true, false, null, null);
    }
}
