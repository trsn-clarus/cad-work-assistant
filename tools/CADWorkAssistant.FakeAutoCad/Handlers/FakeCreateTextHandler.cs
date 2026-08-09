using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>Milestone 12 §95 - 실제 DBText/MText를 만들지 않는다. Content/Height 검증은 실제
/// Handler(CreateTextHandler)와 같은 Core 검증기를 그대로 써서, Integration.Tests가 실제 Named
/// Pipe로 "잘못된 요청 → InvalidRequest" 흐름까지 검증할 수 있게 한다.</summary>
internal sealed class FakeCreateTextHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeCreateTextHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.CreateText;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<CreateTextRequest>(IpcJson.Options);
        if (request is null)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "요청 형식이 올바르지 않습니다.")));
        }

        if (!TextContentValidator.IsValid(request.Content))
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "문자 내용을 입력해주세요.")));
        }

        if (!TextHeightValidator.IsValid(request.Height))
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "높이는 0보다 커야 합니다.")));
        }

        switch (_scenario.TextCreateBehavior)
        {
            case TextWriteBehavior.Error:
                return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, "Simulated AutoCAD internal error.")));

            case TextWriteBehavior.DisconnectBeforeResponding:
                Environment.Exit(0);
                return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InternalError, "unreachable")));

            case TextWriteBehavior.InvalidHandle:
            case TextWriteBehavior.LockedLayer:
            case TextWriteBehavior.Succeed:
            default:
                var created = new CadTextObjectDto(
                    handle: "FAKE" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant(),
                    entityType: request.EntityType,
                    content: request.Content,
                    plainText: request.Content,
                    layerName: request.LayerName ?? "0",
                    height: request.Height,
                    rotation: 0,
                    color: request.Color ?? CadColorPalette.ByLayer,
                    textStyleName: "Standard",
                    isLocked: false,
                    isAnnotative: false,
                    hasInlineFormatting: false);

                return Task.FromResult(IpcHandlerResult.Ok(new CreateTextResponse(created)));
        }
    }
}
