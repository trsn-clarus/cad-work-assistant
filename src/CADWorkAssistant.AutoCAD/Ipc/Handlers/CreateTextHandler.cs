using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// Milestone 12 §35, §90 - 새 DBText 또는 MText 하나를 만든다. LayerName/Color가 없으면 현재
/// Layer(§34)/ByLayer(§26)를 쓴다. 새 엔티티를 Database.CurrentSpaceId(현재 Model/Paper Space
/// Block)에 추가한다 - 이 값을 하드코딩하지 않고 항상 조회한다(Layout에 따라 Model Space일 수도
/// Paper Space일 수도 있다). 하나의 Transaction을 한 번만 Commit하므로 AutoCAD Undo 스택에는 한
/// 항목으로 남는다(§51-52, §84 - Managed API에는 별도 Undo Mark API가 없다는 것을 리플렉션으로
/// 확인했다, Transaction Commit 자체가 이미 한 Undo 단계다).
/// </summary>
internal sealed class CreateTextHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public CreateTextHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.CreateText;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<CreateTextRequest>(IpcJson.Options);
        if (request is null)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "요청 형식이 올바르지 않습니다."));
        }

        if (!TextContentValidator.IsValid(request.Content))
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "문자 내용을 입력해주세요."));
        }

        if (!TextHeightValidator.IsValid(request.Height))
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "높이는 0보다 커야 합니다."));
        }

        var outcome = await _dispatcher.InvokeAsync(() => Create(request), cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            { Success: true } => IpcHandlerResult.Ok(new CreateTextResponse(outcome.Created!)),
            { NoActiveDocument: true } => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            { IsInvalidRequest: true } => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.InvalidRequest, outcome.ErrorMessage ?? "Invalid create-text request.")),
            _ => IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, outcome.ErrorMessage ?? "Text creation failed."))
        };
    }

    private static CreateTextOutcome Create(CreateTextRequest request)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return CreateTextOutcome.NoDocument();
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        using var transaction = database.TransactionManager.StartTransaction();

        string resolvedLayerName;
        if (request.LayerName is not null)
        {
            var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (!layerTable.Has(request.LayerName))
            {
                return CreateTextOutcome.InvalidRequestFailure($"Layer '{request.LayerName}'를 찾을 수 없습니다.");
            }

            var layerRecord = (LayerTableRecord)transaction.GetObject(layerTable[request.LayerName], OpenMode.ForRead);
            if (layerRecord.IsLocked)
            {
                return CreateTextOutcome.InvalidRequestFailure($"Layer '{request.LayerName}'가 잠겨 있어 새 문자를 작성할 수 없습니다.");
            }

            resolvedLayerName = layerRecord.Name;
        }
        else
        {
            var currentLayerRecord = (LayerTableRecord)transaction.GetObject(database.Clayer, OpenMode.ForRead);
            resolvedLayerName = currentLayerRecord.Name;
        }

        var point = new Point3d(request.InsertionPoint.X, request.InsertionPoint.Y, request.InsertionPoint.Z);

        Entity newEntity = request.EntityType == CadTextEntityType.SingleLine
            ? new DBText { TextString = request.Content, Height = request.Height, Position = point, Layer = resolvedLayerName }
            : new MText { Contents = request.Content, TextHeight = request.Height, Location = point, Layer = resolvedLayerName };

        var appended = false;
        try
        {
            if (request.Color is not null)
            {
                newEntity.Color = AutoCadTextEntityAdapter.ToAutoCadColor(request.Color);
            }

            var blockTableRecord = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
            blockTableRecord.AppendEntity(newEntity);
            transaction.AddNewlyCreatedDBObject(newEntity, true);
            appended = true;

            // Commit 전에 DTO를 만든다 - Transaction이 아직 확실히 열려 있는 시점에 Layer Lock 조회 등을
            // 끝낸다(Milestone 11의 PlotDrawingPdfHandler와 같은 이유).
            var dto = AutoCadTextEntityAdapter.BuildDto(newEntity, transaction);
            transaction.Commit();

            return CreateTextOutcome.Succeeded(dto);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            // Transaction을 Commit하지 않았으므로 using이 끝나며 Abort된다 - 이미 append된 엔티티도
            // 함께 버려진다. append 되기 전에 실패했다면(예: 색상 지정 단계) newEntity가 아직 어떤
            // Database에도 속하지 않으므로 직접 Dispose해야 leak이 없다.
            if (!appended)
            {
                newEntity.Dispose();
            }

            return CreateTextOutcome.Failed(ex.Message);
        }
    }

    private sealed class CreateTextOutcome
    {
        private CreateTextOutcome(bool success, bool noActiveDocument, bool isInvalidRequest, CadTextObjectDto? created, string? errorMessage)
        {
            Success = success;
            NoActiveDocument = noActiveDocument;
            IsInvalidRequest = isInvalidRequest;
            Created = created;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public bool NoActiveDocument { get; }
        public bool IsInvalidRequest { get; }
        public CadTextObjectDto? Created { get; }
        public string? ErrorMessage { get; }

        public static CreateTextOutcome Succeeded(CadTextObjectDto created) => new(true, false, false, created, null);
        public static CreateTextOutcome Failed(string message) => new(false, false, false, null, message);
        public static CreateTextOutcome InvalidRequestFailure(string message) => new(false, false, true, null, message);
        public static CreateTextOutcome NoDocument() => new(false, true, false, null, null);
    }
}
