using System;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;
using Serilog;

namespace CADWorkAssistant.Desktop.Services;

public sealed class DrawingPdfExportCoordinator : IDrawingPdfExportCoordinator
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly IProjectContextService _projectContext;

    public DrawingPdfExportCoordinator(IAutoCadConnectionManager connectionManager, IProjectContextService projectContext)
    {
        _connectionManager = connectionManager;
        _projectContext = projectContext;
    }

    public async Task<DrawingPdfExportOutcome> ExportAsync(PlotDrawingPdfRequest request, CancellationToken cancellationToken)
    {
        var response = await _connectionManager
            .SendRequestAsync(IpcMessageTypes.PlotDrawingPdf, request, cancellationToken)
            .ConfigureAwait(true);

        if (!response.Success)
        {
            return DrawingPdfExportOutcome.Failed(response.Error!);
        }

        var result = response.DeserializePayload<PlotDrawingPdfResponse>();
        if (result is null)
        {
            return DrawingPdfExportOutcome.Failed(new IpcError(IpcErrorCode.InternalError, "Empty response payload."));
        }

        try
        {
            var scopeText = request.Scope == CadPlotScope.Window ? "영역 출력" : "Layout 출력";
            await _projectContext.AddDrawingPdfExportRecordAsync(
                result.OutputFile, pageCount: 1, $"{scopeText} · {result.ResolvedMediaDisplay}");
        }
        catch (Exception ex)
        {
            // 파일은 이미 성공적으로 생성됐다 - DB 기록 실패를 Plot 실패로 취급하지 않는다
            // (QuantityPdfExportCoordinator와 같은 원칙).
            Log.Error(ex, "Failed to record drawing PDF export history");
        }

        return DrawingPdfExportOutcome.Succeeded(result);
    }
}
