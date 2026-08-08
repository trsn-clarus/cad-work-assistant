using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// 선택한 객체를 WBLOCK으로 새 DWG에 저장한다 (§49-52). Database.Wblock(ObjectIdCollection, Point3d)은
/// 원본 Database를 건드리지 않고 완전히 새 in-memory Database를 만들어 돌려준다 - 여기에 SaveAs만
/// 하면 원본은 그대로다(§52, §63, §124). 리플렉션으로 실제 시그니처 존재를 확인함(Milestone 5 §51).
/// Layer/Linetype/Style 등 Dependency는 Wblock이 자동으로 함께 담아온다 - 실제 정확성은 Real
/// AutoCAD에서만 확인 가능하다(§59, docs/AUTOCAD_REAL_MACHINE_CHECKLIST.md).
/// </summary>
internal sealed class ExportSelectionHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public ExportSelectionHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.ExportSelection;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var request = payload?.Deserialize<ExportSelectionRequest>(IpcJson.Options);
        if (request is null || request.Handles.Count == 0)
        {
            return IpcHandlerResult.Fail(new IpcError(IpcErrorCode.InvalidRequest, "ExportSelection requires at least one handle."));
        }

        var outcome = await _dispatcher.InvokeAsync(() => Export(request), cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            { Success: true } => IpcHandlerResult.Ok(new ExportSelectionResponse(outcome.ObjectCount, request.TargetFilePath)),
            { NoActiveDocument: true } => IpcHandlerResult.Fail(
                new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document.")),
            _ => IpcHandlerResult.Fail(new IpcError(IpcErrorCode.ApiExecutionFailed, outcome.ErrorMessage ?? "Export failed."))
        };
    }

    private static ExportOutcome Export(ExportSelectionRequest request)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return ExportOutcome.NoDocument();
        }

        using var documentLock = document.LockDocument();
        var database = document.Database;

        var objectIds = new ObjectIdCollection();

        using (var transaction = database.TransactionManager.StartTransaction())
        {
            foreach (var handleText in request.Handles)
            {
                if (!long.TryParse(handleText, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var handleValue))
                {
                    continue;
                }

                if (database.TryGetObjectId(new Handle(handleValue), out var objectId) && !objectId.IsErased)
                {
                    objectIds.Add(objectId);
                }
            }

            // Read-only: 원본 Database에는 아무 것도 Commit하지 않는다 - Wblock 자체가 별도
            // Database를 만든다.
        }

        if (objectIds.Count == 0)
        {
            return ExportOutcome.Failed("No valid objects to export.");
        }

        try
        {
            var directory = Path.GetDirectoryName(request.TargetFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                return ExportOutcome.Failed($"Target folder does not exist: {directory}");
            }

            using var outputDatabase = database.Wblock(objectIds, Point3d.Origin);
            outputDatabase.SaveAs(request.TargetFilePath, DwgVersion.Current);

            return ExportOutcome.Succeeded(objectIds.Count);
        }
        catch (Exception ex) when (ex is Autodesk.AutoCAD.Runtime.Exception or IOException or UnauthorizedAccessException)
        {
            return ExportOutcome.Failed(ex.Message);
        }
    }

    private sealed class ExportOutcome
    {
        private ExportOutcome(bool success, bool noActiveDocument, int objectCount, string? errorMessage)
        {
            Success = success;
            NoActiveDocument = noActiveDocument;
            ObjectCount = objectCount;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public bool NoActiveDocument { get; }
        public int ObjectCount { get; }
        public string? ErrorMessage { get; }

        public static ExportOutcome Succeeded(int objectCount) => new(true, false, objectCount, null);
        public static ExportOutcome Failed(string message) => new(false, false, 0, message);
        public static ExportOutcome NoDocument() => new(false, true, 0, null);
    }
}
