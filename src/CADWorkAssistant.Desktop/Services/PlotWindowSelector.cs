using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Desktop.Services;

internal enum PlotWindowOutcomeKind
{
    Selected,
    Cancelled,
    Error
}

/// <summary>LengthSelectionCoordinator/AutoCadDispatcher의 SelectionOutcome&lt;T&gt;와 같은 이유로
/// 같은 모양을 쓴다 (Milestone 11 §13).</summary>
internal sealed class PlotWindowOutcome
{
    private PlotWindowOutcome(PlotWindowOutcomeKind kind, CadPlotWindowDto? window, IpcError? error)
    {
        Kind = kind;
        Window = window;
        Error = error;
    }

    public PlotWindowOutcomeKind Kind { get; }

    public CadPlotWindowDto? Window { get; }

    public IpcError? Error { get; }

    public static PlotWindowOutcome Selected(CadPlotWindowDto window) => new(PlotWindowOutcomeKind.Selected, window, null);

    public static PlotWindowOutcome Cancelled() => new(PlotWindowOutcomeKind.Cancelled, null, null);

    public static PlotWindowOutcome Failed(IpcError error) => new(PlotWindowOutcomeKind.Error, null, error);
}

/// <summary>AcquirePlotWindow IPC 호출을 감싼다 - LengthSelectionCoordinator와 같은 이유로 정적
/// 헬퍼 하나로 충분하다(§13, §54).</summary>
internal static class PlotWindowSelector
{
    public static async Task<PlotWindowOutcome> SelectAsync(IAutoCadConnectionManager connectionManager, CancellationToken cancellationToken)
    {
        var response = await connectionManager
            .SendRequestAsync(IpcMessageTypes.AcquirePlotWindow, payload: null, cancellationToken)
            .ConfigureAwait(true);

        if (!response.Success)
        {
            return response.Error!.Code == IpcErrorCode.SelectionCancelled
                ? PlotWindowOutcome.Cancelled()
                : PlotWindowOutcome.Failed(response.Error!);
        }

        var result = response.DeserializePayload<AcquirePlotWindowResponse>();
        return result is null
            ? PlotWindowOutcome.Failed(new IpcError(IpcErrorCode.InternalError, "Empty response payload."))
            : PlotWindowOutcome.Selected(result.Window);
    }
}
