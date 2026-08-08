using System.Diagnostics;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Length;

/// <summary>
/// Milestone 2 §64: 1,000개 객체를 선택해도 UI가 멈춘 것처럼 보이지 않아야 한다는 요구사항을,
/// 실제 IPC 왕복이 합리적인 시간 안에 끝나는지로 대신 확인한다 (진짜 UI freeze는 Desktop을 띄워야
/// 확인 가능하므로 자동화 범위 밖 - §55 참고).
/// </summary>
public class LargeSelectionPerformanceTests
{
    [Fact]
    public async Task LargeSelection_1000Objects_CompletesQuicklyAndAggregatesCorrectly()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("LargeSelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, requestTimeoutMs: 5000, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(response.Success);
        Assert.True(stopwatch.ElapsedMilliseconds < 3000, $"IPC round trip took {stopwatch.ElapsedMilliseconds}ms for 1000 objects - too slow.");

        var selection = response.DeserializePayload<LengthSelectionResponse>();
        Assert.Equal(1000, selection!.Objects.Count);

        var result = LengthAggregationService.Aggregate(selection, "large.dwg", DateTimeOffset.Now);
        Assert.Equal(1000, result.ObjectCount);
        Assert.NotNull(result.DisplayValueMeters);
    }
}
