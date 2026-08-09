using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>Milestone 11 §49-50, §55 - PlotDrawingPdf IPC 호출 + 성공 시 ExportRecord/Activity
/// 저장을 하나로 묶는다. IQuantityPdfExportCoordinator(Milestone 10)와 다르게 Persistence만이
/// 아니라 IAutoCadConnectionManager도 함께 다룬다 - 실제 Plot 자체가 AutoCAD IPC 호출이기
/// 때문이다(ExportWorkflowViewModel의 DWG Export와 같은 성격).</summary>
public interface IDrawingPdfExportCoordinator
{
    Task<DrawingPdfExportOutcome> ExportAsync(PlotDrawingPdfRequest request, CancellationToken cancellationToken);
}

public sealed class DrawingPdfExportOutcome
{
    private DrawingPdfExportOutcome(bool success, PlotDrawingPdfResponse? response, IpcError? error)
    {
        Success = success;
        Response = response;
        Error = error;
    }

    public bool Success { get; }

    public PlotDrawingPdfResponse? Response { get; }

    public IpcError? Error { get; }

    public static DrawingPdfExportOutcome Succeeded(PlotDrawingPdfResponse response) => new(true, response, null);

    public static DrawingPdfExportOutcome Failed(IpcError error) => new(false, null, error);
}
