using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>
/// Milestone 12 §95, §106 - 실제 DWG를 편집하지 않지만, Batch Patch Semantics는 실제로 검증할 수
/// 있게 <see cref="_scenario"/>.TextObjects 중 요청받은 handle에 대응하는 항목에 실제로 patch를
/// 적용해서 돌려준다(값만 바뀐 새 DTO - Core.Text에는 mutable 타입이 없다). 렌더링/Undo는 검증
/// 대상이 아니다(§95).
/// </summary>
internal sealed class FakeUpdateTextObjectsHandler : IIpcRequestHandler
{
    private readonly SimulationScenario _scenario;

    public FakeUpdateTextObjectsHandler(SimulationScenario scenario)
    {
        _scenario = scenario;
    }

    public string MessageType => IpcMessageTypes.UpdateTextObjects;

    public Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<UpdateTextObjectsRequest>(IpcJson.Options);
        if (request is null || request.Handles.Count == 0)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "수정할 문자를 1개 이상 선택해주세요.")));
        }

        if (!request.Patch.HasAnyChange)
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "변경할 항목을 1개 이상 선택해주세요.")));
        }

        if (request.Patch.Content.HasValue && !TextContentValidator.IsValid(request.Patch.Content.Value))
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "문자 내용을 입력해주세요.")));
        }

        if (request.Patch.Height.HasValue && !TextHeightValidator.IsValid(request.Patch.Height.Value))
        {
            return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "높이는 0보다 커야 합니다.")));
        }

        switch (_scenario.TextUpdateBehavior)
        {
            case TextWriteBehavior.InvalidHandle:
                return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "일부 문자 객체를 찾을 수 없습니다. 다시 선택해주세요.")));

            case TextWriteBehavior.LockedLayer:
                // Real UpdateTextObjectsHandler와 같은 분류(InvalidRequest) - Desktop DescribeError가
                // 이 메시지를 그대로 보여줄 수 있다(§55, ApiExecutionFailed는 raw 예외용으로 남겨둔다).
                return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "Layer가 잠겨 있어 이 Layer의 문자는 수정할 수 없습니다.")));

            case TextWriteBehavior.Error:
                return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, "Simulated AutoCAD internal error.")));

            case TextWriteBehavior.DisconnectBeforeResponding:
                Environment.Exit(0);
                return Task.FromResult(IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InternalError, "unreachable")));

            case TextWriteBehavior.Succeed:
            default:
                var updated = new List<CadTextObjectDto>();
                foreach (var handle in request.Handles)
                {
                    var original = _scenario.TextObjects.FirstOrDefault(o => o.Handle == handle);
                    if (original is null)
                    {
                        continue;
                    }

                    updated.Add(ApplyPatch(original, request.Patch));
                }

                return Task.FromResult(IpcHandlerResult.Ok(new UpdateTextObjectsResponse(updated)));
        }
    }

    private static CadTextObjectDto ApplyPatch(CadTextObjectDto original, TextUpdatePatch patch)
    {
        var content = patch.Content.HasValue ? patch.Content.Value! : original.Content;
        return new CadTextObjectDto(
            original.Handle,
            original.EntityType,
            content: content,
            plainText: content,
            layerName: patch.LayerName.HasValue ? patch.LayerName.Value! : original.LayerName,
            height: patch.Height.HasValue ? patch.Height.Value : original.Height,
            rotation: original.Rotation,
            color: patch.Color.HasValue ? patch.Color.Value! : original.Color,
            textStyleName: original.TextStyleName,
            isLocked: original.IsLocked,
            isAnnotative: original.IsAnnotative,
            hasInlineFormatting: original.HasInlineFormatting);
    }
}
