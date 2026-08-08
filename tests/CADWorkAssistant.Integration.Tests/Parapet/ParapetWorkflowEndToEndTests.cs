using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Core.Parapet;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Parapet;

/// <summary>
/// Milestone 4 §56, §99, §106-107: Parapet도 Vertical Area와 마찬가지로 새 AutoCAD IPC 명령이
/// 없다 - 기존 "NormalSelection" Length Scenario를 그대로 재사용한다(§54). Parapet의 exact worked
/// example(32.118 m 등)은 이미 Core.Tests의 ParapetCalculatorTests에서 AutoCAD 없이 정밀 검증했으므로,
/// 여기서는 다른 길이 값(255.940660 m)으로 "실제 IPC 왕복 -> Core 조합 계산"이라는 배선 자체가
/// 맞는지를 확인한다.
/// </summary>
public class ParapetWorkflowEndToEndTests
{
    [Fact]
    public async Task FullWorkflow_CadLengthBothFacesPlusTop_ComputesCorrectTotal()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("NormalSelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(selectionResponse.Success);

        var selection = selectionResponse.DeserializePayload<LengthSelectionResponse>();
        var lengthResult = LengthAggregationService.Aggregate(selection!, "School_Roof.dwg", DateTimeOffset.Now);
        Assert.NotNull(lengthResult.DisplayValueMeters);

        var input = new ParapetInput(
            lengthResult.DisplayValueMeters!.Value,
            heightRawValue: 1.0,
            DrawingUnit.Meters,
            ParapetFaceMode.Both,
            topIncluded: true,
            topWidthRawValue: 0.15,
            DrawingUnit.Meters);

        var result = ParapetCalculator.Calculate(input, DateTimeOffset.Now);

        Assert.Equal(511.881320, result.SideAreaSquareMeters, precision: 5);
        Assert.Equal(38.391099, result.TopAreaSquareMeters, precision: 5);
        Assert.Equal(550.272419, result.TotalAreaSquareMeters, precision: 5);
        Assert.Equal("550.272 m²", AreaFormatter.FormatSquareMetersWithUnit(result.TotalAreaSquareMeters, decimalPlaces: 3));
    }

    [Fact]
    public async Task FullWorkflow_SingleFaceNoTop_MatchesSourceLengthTimesHeight()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("NormalSelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = selectionResponse.DeserializePayload<LengthSelectionResponse>();
        var lengthResult = LengthAggregationService.Aggregate(selection!, "School_Roof.dwg", DateTimeOffset.Now);

        var input = new ParapetInput(
            lengthResult.DisplayValueMeters!.Value, 1.0, DrawingUnit.Meters, ParapetFaceMode.Single, false, 0, DrawingUnit.Meters);

        var result = ParapetCalculator.Calculate(input, DateTimeOffset.Now);

        Assert.Equal(lengthResult.DisplayValueMeters.Value, result.TotalAreaSquareMeters, precision: 6);
    }

    [Fact]
    public void FullWorkflow_ManualLengthSource_DoesNotNeedAutoCad()
    {
        // §85 예시 값(32.118 m, 1.0 m, Both, 0.15 m -> 69.054 m²)은 Core.Tests에서 이미 검증했다 -
        // 여기서는 "수동 입력 소스는 IPC를 전혀 타지 않는다"는 것만 확인한다.
        var input = new ParapetInput(32.118, 1.0, DrawingUnit.Meters, ParapetFaceMode.Both, true, 0.150, DrawingUnit.Meters);

        var result = ParapetCalculator.Calculate(input, DateTimeOffset.Now);

        Assert.Equal("69.054 m²", AreaFormatter.FormatSquareMetersWithUnit(result.TotalAreaSquareMeters, decimalPlaces: 3));
    }
}
