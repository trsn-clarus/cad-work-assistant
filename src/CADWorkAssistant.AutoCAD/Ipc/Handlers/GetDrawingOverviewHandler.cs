using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using Document = Autodesk.AutoCAD.ApplicationServices.Document;

namespace CADWorkAssistant.AutoCAD.Ipc.Handlers;

/// <summary>
/// Drawing Navigation의 최소 기반 - 전체 Extents/객체 수/Layer 수 (Milestone 5 §64). Read-only,
/// ApplicationContext로 충분하다(사용자 입력을 기다리지 않는다).
/// </summary>
internal sealed class GetDrawingOverviewHandler : IIpcRequestHandler
{
    private readonly IAutoCadDispatcher _dispatcher;

    public GetDrawingOverviewHandler(IAutoCadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public string MessageType => IpcMessageTypes.GetDrawingOverview;

    public async Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken)
    {
        var overview = await _dispatcher.InvokeAsync(BuildOverviewOrNull, cancellationToken).ConfigureAwait(false);

        return overview is null
            ? IpcHandlerResult.Fail(new IpcError(IpcErrorCode.NoActiveDocument, "AutoCAD has no open document."))
            : IpcHandlerResult.Ok(overview);
    }

    private static DrawingOverviewResponse? BuildOverviewOrNull()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return null;
        }

        using var documentLock = document.LockDocument();
        return BuildOverviewForZoom(document);
    }

    /// <summary>ZoomExtentsHandler가 이미 Document Lock을 잡은 상태에서 재사용한다 - Zoom Extents가
    /// "지금 실제로 뭐가 보이는지"를 다시 계산해야 하는데, Extents 계산 로직을 두 곳에 복붙하지
    /// 않기 위해서다.</summary>
    internal static DrawingOverviewResponse BuildOverviewForZoom(Document document)
    {
        var database = document.Database;

        using var transaction = database.TransactionManager.StartTransaction();

        var modelSpace = (BlockTableRecord)transaction.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        // Database.Extmin/Extmax는 마지막 Regen 시점 값이라 지금 실제 도면과 어긋날 수 있다 - 매번
        // 직접 순회해서 계산한다. 객체 수도 어차피 같은 순회에서 셀 수 있어 비용이 추가로 들지 않는다.
        var boundsList = new System.Collections.Generic.List<CadBoundsDto?>();
        var objectCount = 0;

        foreach (var objectId in modelSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            objectCount++;
            boundsList.Add(TryGetBounds(entity));
        }

        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        var layerCount = layerTable.Cast<ObjectId>().Count();

        return new DrawingOverviewResponse(BoundsAggregator.Aggregate(boundsList), objectCount, layerCount);
    }

    // GeometricExtents는 일부 Entity(예: 빈 Block Reference)에서 예외를 던질 수 있다 - 그런 객체는
    // Bounds 없이 개수에만 포함시킨다.
    internal static CadBoundsDto? TryGetBounds(Entity entity)
    {
        try
        {
            var extents = entity.GeometricExtents;
            return new CadBoundsDto(
                extents.MinPoint.X, extents.MinPoint.Y, extents.MinPoint.Z,
                extents.MaxPoint.X, extents.MaxPoint.Y, extents.MaxPoint.Z);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return null;
        }
    }
}
