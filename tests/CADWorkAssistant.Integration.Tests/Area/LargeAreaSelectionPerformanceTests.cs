using System.Diagnostics;
using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Area;

/// <summary>
/// Milestone 3 §35, §81: 1,000개 닫힌 Polyline을 선택해도 크래시/UI freeze/IPC timeout 없이 처리돼야
/// 한다. Length의 LargeSelectionPerformanceTests와 같은 방식으로, 실제 IPC 왕복 시간으로 대신
/// 확인한다 (진짜 UI freeze는 Desktop을 띄워야 확인 가능하므로 자동화 범위 밖).
/// </summary>
public class LargeAreaSelectionPerformanceTests
{
    [Fact]
    public async Task LargeSelection_1000Objects_CompletesQuicklyAndAggregatesCorrectly()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("LargeAreaSelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, requestTimeoutMs: 5000, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(response.Success);
        Assert.True(stopwatch.ElapsedMilliseconds < 3000, $"IPC round trip took {stopwatch.ElapsedMilliseconds}ms for 1000 objects - too slow.");

        var selection = response.DeserializePayload<AreaSelectionResponse>();
        Assert.Equal(1000, selection!.Objects.Count);

        var result = AreaAggregationService.Aggregate(selection, "large.dwg", DateTimeOffset.Now);
        Assert.Equal(1000, result.SupportedCount);
        Assert.NotNull(result.DisplayValueSquareMeters);
    }
}
