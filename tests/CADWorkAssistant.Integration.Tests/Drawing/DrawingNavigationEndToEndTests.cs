using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Drawing;

/// <summary>
/// Milestone 5 §109 E2E A: Select → Zoom → Isolate → Restore, 전부 실제 Named Pipe로 두 프로세스
/// 사이에서 검증한다. AutoCAD는 필요 없다.
/// </summary>
public class DrawingNavigationEndToEndTests
{
    [Fact]
    public async Task FullWorkflow_Overview_Select_Zoom_Isolate_Restore()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingNavigationNormal", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var overviewResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetDrawingOverview, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(overviewResponse.Success);
        var overview = overviewResponse.DeserializePayload<DrawingOverviewResponse>();
        Assert.Equal(6, overview!.ObjectCount);
        Assert.Equal(5, overview.LayerCount);
        Assert.NotNull(overview.Extents);

        var zoomExtentsResponse = await client.SendRequestAsync(
            IpcMessageTypes.ZoomExtents, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(zoomExtentsResponse.Success);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectDrawingObjects,
            new SelectDrawingObjectsRequest(SelectionMode.Crossing),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);
        Assert.True(selectionResponse.Success);
        var selection = selectionResponse.DeserializePayload<DrawingSelectionResponse>();
        Assert.Equal(6, selection!.Objects.Count);

        // Core로 SelectionSession을 만들어 Bounds/요약을 계산한다 - AutoCAD Plugin은 원본만 준다.
        var session = SelectionSession.Create("School_Roof.dwg", selection.Objects, DateTimeOffset.Now);
        Assert.Equal(6, session.ObjectCount);
        Assert.NotNull(session.Bounds);
        Assert.True(session.TypeCounts.Count > 1);

        var zoomToBoundsResponse = await client.SendRequestAsync(
            IpcMessageTypes.ZoomToBounds,
            new ZoomToBoundsRequest(session.Bounds!),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);
        Assert.True(zoomToBoundsResponse.Success);

        var isolateResponse = await client.SendRequestAsync(
            IpcMessageTypes.IsolateObjects,
            new IsolateObjectsRequest(session.Handles),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);
        Assert.True(isolateResponse.Success);

        var restoreResponse = await client.SendRequestAsync(
            IpcMessageTypes.RestoreVisibility, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(restoreResponse.Success);
    }

    [Fact]
    public async Task RestoreVisibility_WithNoActiveIsolation_SucceedsAsNoOp()
    {
        // §98: 아무것도 바뀐 게 없을 때 눌러도 오류가 아니다.
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingNavigationNormal", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.RestoreVisibility, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetDrawingOverview_EmptyDrawing_ReturnsNullExtentsWithZeroCount()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingEmptySelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.GetDrawingOverview, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        var overview = response.DeserializePayload<DrawingOverviewResponse>();
        Assert.Equal(0, overview!.ObjectCount);
        Assert.Null(overview.Extents);
    }
}
