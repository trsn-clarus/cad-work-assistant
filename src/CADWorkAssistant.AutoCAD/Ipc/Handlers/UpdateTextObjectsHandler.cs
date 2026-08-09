using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// Milestone 12 §53-55, §91 - 선택한 문자 객체들에 <see cref="TextUpdatePatch"/>를 한 번에 적용한다.
/// all-or-nothing이다(§53) - handle 유효성/대상 Layer 존재·Lock 여부를 전부 먼저 검증(§54)하고,
/// 하나라도 실패하면 실제 쓰기 자체를 시작하지 않는다. 실제 쓰기 도중 예외가 나도 Transaction을
/// Commit하지 않으므로(using이 Abort) 일부만 바뀐 상태가 남지 않는다. 여러 객체를 하나의 Transaction
/// 안에서 한 번만 Commit하므로 AutoCAD Undo 스택에는 한 항목으로 남는다(§51-52).
/// </summary>
internal sealed class UpdateTextObjectsHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public UpdateTextObjectsHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.UpdateTextObjects;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<UpdateTextObjectsRequest>(IpcJson.Options);
        if (request is null || request.Handles.Count == 0)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "수정할 문자를 1개 이상 선택해주세요."));
        }

        if (!request.Patch.HasAnyChange)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "변경할 항목을 1개 이상 선택해주세요."));
        }

        if (request.Patch.Content.HasValue && !TextContentValidator.IsValid(request.Patch.Content.Value))
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "문자 내용을 입력해주세요."));
        }

        if (request.Patch.Height.HasValue && !TextHeightValidator.IsValid(request.Patch.Height.Value))
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "높이는 0보다 커야 합니다."));
        }

        var outcome = await _dispatcher.InvokeAsync(() => Apply(request), cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            { Success: true } => IpcHandlerResult.Ok(new UpdateTextObjectsResponse(outcome.UpdatedObjects!)),
            { NoActiveDocument: true } => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            { IsInvalidRequest: true } => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.InvalidRequest, outcome.ErrorMessage ?? "Invalid update request.")),
            _ => IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, outcome.ErrorMessage ?? "Text update failed."))
        };
    }

    private static UpdateTextOutcome Apply(UpdateTextObjectsRequest request)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return UpdateTextOutcome.NoDocument();
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        using var transaction = database.TransactionManager.StartTransaction();

        // §54: Validation 먼저 - handle 유효성 + 현재 Layer Lock 여부를 전부 확인한 뒤에만 실제로 쓴다.
        var entities = new List<Entity>();
        foreach (var handleText in request.Handles)
        {
            if (!long.TryParse(handleText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var handleValue) ||
                !database.TryGetObjectId(new Handle(handleValue), out var objectId) ||
                objectId.IsErased)
            {
                return UpdateTextOutcome.InvalidRequestFailure("일부 문자 객체를 찾을 수 없습니다. 다시 선택해주세요.");
            }

            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity || !AutoCadTextEntityAdapter.IsSupported(entity))
            {
                return UpdateTextOutcome.InvalidRequestFailure("일부 문자 객체를 찾을 수 없습니다. 다시 선택해주세요.");
            }

            // §33, §55: 대상 객체가 Locked Layer에 있으면 이 객체는 편집할 수 없다 - 하나라도 걸리면
            // 전체 실패시킨다(부분 수정 금지, §53).
            if (transaction.GetObject(entity.LayerId, OpenMode.ForRead) is LayerTableRecord { IsLocked: true } currentLayer)
            {
                // InvalidHandle과 같은 부류(요청을 이 상태로는 처리할 수 없음)로 분류한다 - Desktop이
                // 이 메시지를 그대로 보여줄 수 있으려면 raw 예외와 같은 코드(ApiExecutionFailed)를
                // 쓰면 안 된다(CLAUDE.md 절대 원칙 #4, §55).
                return UpdateTextOutcome.InvalidRequestFailure($"'{currentLayer.Name}' Layer가 잠겨 있어 이 Layer의 문자는 수정할 수 없습니다.");
            }

            entities.Add(entity);
        }

        if (request.Patch.LayerName.HasValue)
        {
            var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (!layerTable.Has(request.Patch.LayerName.Value!))
            {
                return UpdateTextOutcome.InvalidRequestFailure($"Layer '{request.Patch.LayerName.Value}'를 찾을 수 없습니다.");
            }

            if (transaction.GetObject(layerTable[request.Patch.LayerName.Value!], OpenMode.ForRead) is LayerTableRecord { IsLocked: true })
            {
                return UpdateTextOutcome.InvalidRequestFailure($"대상 Layer '{request.Patch.LayerName.Value}'가 잠겨 있습니다.");
            }
        }

        try
        {
            var updated = new List<CadTextObjectDto>();
            foreach (var entity in entities)
            {
                entity.UpgradeOpen();
                AutoCadTextEntityAdapter.ApplyPatch(entity, request.Patch);
                updated.Add(AutoCadTextEntityAdapter.BuildDto(entity, transaction));
            }

            transaction.Commit();
            return UpdateTextOutcome.Succeeded(updated);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            // Commit하지 않았으므로 using이 끝나며 전체 Transaction이 Abort된다 - 부분 수정이 남지
            // 않는다(§53).
            return UpdateTextOutcome.Failed(ex.Message);
        }
    }

    private sealed class UpdateTextOutcome
    {
        private UpdateTextOutcome(bool success, bool noActiveDocument, bool isInvalidRequest, IReadOnlyList<CadTextObjectDto>? updatedObjects, string? errorMessage)
        {
            Success = success;
            NoActiveDocument = noActiveDocument;
            IsInvalidRequest = isInvalidRequest;
            UpdatedObjects = updatedObjects;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public bool NoActiveDocument { get; }
        public bool IsInvalidRequest { get; }
        public IReadOnlyList<CadTextObjectDto>? UpdatedObjects { get; }
        public string? ErrorMessage { get; }

        public static UpdateTextOutcome Succeeded(IReadOnlyList<CadTextObjectDto> updatedObjects) => new(true, false, false, updatedObjects, null);
        public static UpdateTextOutcome Failed(string message) => new(false, false, false, null, message);
        public static UpdateTextOutcome InvalidRequestFailure(string message) => new(false, false, true, null, message);
        public static UpdateTextOutcome NoDocument() => new(false, true, false, null, null);
    }
}
