using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.Desktop.Services;

public sealed class PlotCapabilityCoordinator : IPlotCapabilityCoordinator
{
    private readonly IAutoCadConnectionManager _connectionManager;

    public PlotCapabilityCoordinator(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<PlotCapabilityLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var response = await _connectionManager
            .SendRequestAsync(IpcMessageTypes.GetPlotCapabilities, payload: null, cancellationToken)
            .ConfigureAwait(true);

        if (!response.Success)
        {
            return PlotCapabilityLoadResult.Failed(response.Error!);
        }

        var capabilities = response.DeserializePayload<PlotCapabilitiesResponse>();
        return capabilities is null
            ? PlotCapabilityLoadResult.Failed(new IpcError(IpcErrorCode.InternalError, "Empty response payload."))
            : PlotCapabilityLoadResult.Loaded(capabilities);
    }
}
