using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>
/// 실제 WBLOCK을 수행하지 않는다 - 그 정확성은 Real AutoCAD에서만 검증 가능하다(§69, §78). 대신
/// 대상 경로에 자리표시 파일을 실제로 써서 "Desktop → IPC → 파일시스템" 배관 자체는 진짜 Named
/// Pipe/파일 I/O로 검증한다(§72 - Core/Infrastructure 파일 처리 검증, DWG Binary 정확성 주장 아님).
/// </summary>
internal sealed class FakeExportSelectionHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeExportSelectionHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.ExportSelection;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<ExportSelectionRequest>(IpcJson.Options);
        if (request is null || request.Handles.Count == 0)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "ExportSelection requires at least one handle.")));
        }

        if (_scenario.ExportShouldFail)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, "Simulated WBLOCK failure.")));
        }

        try
        {
            File.WriteAllText(request.TargetFilePath, "CADWorkAssistant FakeAutoCad placeholder export - not a real DWG.");
        }
        catch (IOException ex)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, ex.Message)));
        }

        return Task.FromResult(IpcHandlerResult.Ok(new ExportSelectionResponse(request.Handles.Count, request.TargetFilePath)));
    }
}
