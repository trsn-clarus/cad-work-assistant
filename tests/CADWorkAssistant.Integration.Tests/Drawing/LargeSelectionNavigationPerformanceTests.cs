using System.Diagnostics;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Drawing;

/// <summary>Milestone 5 §127: 1,000개 객체 Selection + Bounds 집계가 합리적인 시간 안에 끝나는지
/// 확인한다 (진짜 UI freeze 여부는 Desktop을 띄워야 확인 가능 - 자동화 범위 밖).</summary>
public class LargeSelectionNavigationPerformanceTests
{
    [Fact]
    public async Task LargeSelection_1000Objects_CompletesQuicklyAndAggregatesBounds()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("LargeSelectionNavigation", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectDrawingObjects,
            new SelectDrawingObjectsRequest(SelectionMode.Crossing),
            requestTimeoutMs: 5000,
            CancellationToken.None);
        stopwatch.Stop();

        Assert.True(response.Success);
        Assert.True(stopwatch.ElapsedMilliseconds < 3000, $"IPC round trip took {stopwatch.ElapsedMilliseconds}ms for 1000 objects - too slow.");

        var selection = response.DeserializePayload<DrawingSelectionResponse>();
        Assert.Equal(1000, selection!.Objects.Count);

        var session = SelectionSession.Create("large.dwg", selection.Objects, DateTimeOffset.Now);
        Assert.Equal(1000, session.ObjectCount);
        Assert.NotNull(session.Bounds);
        Assert.Equal(2, session.TypeCounts.Count);
        Assert.Equal(2, session.LayerCounts.Count);
    }

    [Fact]
    public async Task GetLayers_60Layers_CompletesQuickly()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("LayerListNormal", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.SendRequestAsync(
            IpcMessageTypes.GetLayers, null, requestTimeoutMs: 5000, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(response.Success);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"GetLayers took {stopwatch.ElapsedMilliseconds}ms for 60 layers - too slow.");
    }
}
