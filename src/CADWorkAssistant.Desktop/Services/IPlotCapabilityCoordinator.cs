using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Desktop.Services;

/// <summary>Milestone 11 §68-69 - GetPlotCapabilities IPC 호출을 감싼다. DrawingPdfExportViewModel은
/// Named Pipe 세부사항을 몰라도 된다(§37과 같은 이유 - IAutoCadConnectionManager를 직접 다루지
/// 않는다).</summary>
public interface IPlotCapabilityCoordinator
{
    Task<PlotCapabilityLoadResult> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>성공/실패를 구조화된 타입으로 감싼다 - LengthCoordinatorOutcome과 같은 이유(Milestone 2/3).</summary>
public sealed class PlotCapabilityLoadResult
{
    private PlotCapabilityLoadResult(bool success, PlotCapabilitiesResponse? capabilities, IpcError? error)
    {
        Success = success;
        Capabilities = capabilities;
        Error = error;
    }

    public bool Success { get; }

    public PlotCapabilitiesResponse? Capabilities { get; }

    public IpcError? Error { get; }

    public static PlotCapabilityLoadResult Loaded(PlotCapabilitiesResponse capabilities) => new(true, capabilities, null);

    public static PlotCapabilityLoadResult Failed(IpcError error) => new(false, null, error);
}
