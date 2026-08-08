using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Core.VerticalArea;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.VerticalArea;

/// <summary>
/// Milestone 4 §55, §98, §106-107: Vertical Area는 새 AutoCAD IPC 명령을 쓰지 않는다 - 실제 길이는
/// Milestone 2의 SelectLengthObjects로 그대로 받아온다(여기서는 기존 "NormalSelection" Scenario를
/// 재사용한다, §54). FakeAutoCad는 Length 데이터만 주고, Vertical Area 계산 자체는 전부 Core에서
/// 일어난다 - FakeAutoCad 안에 계산 로직을 넣지 않는다는 원칙(§106)을 그대로 지킨다.
/// </summary>
public class VerticalAreaWorkflowEndToEndTests
{
    [Fact]
    public async Task FullWorkflow_CadLengthPlusHeight_MatchesKnownWorkOrderExample()
    {
        // §55 그대로: 255940.660 mm 길이 -> 255.940660 m, 높이 100mm(0.1m) -> 25.594066 m² -> "25.594 m²"
        await using var fake = await FakeAutoCadProcess.StartAsync("NormalSelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(selectionResponse.Success);

        var selection = selectionResponse.DeserializePayload<LengthSelectionResponse>();
        Assert.NotNull(selection);

        var lengthResult = LengthAggregationService.Aggregate(selection!, "School_Roof.dwg", DateTimeOffset.Now);
        Assert.NotNull(lengthResult.DisplayValueMeters);

        var verticalAreaResult = VerticalAreaCalculator.Calculate(
            lengthResult.DisplayValueMeters!.Value, heightRawValue: 100.0, DrawingUnit.Millimeters, DateTimeOffset.Now);

        Assert.Equal(25.594066, verticalAreaResult.AreaSquareMeters, precision: 6);
        Assert.Equal("25.594 m²", AreaFormatter.FormatSquareMetersWithUnit(verticalAreaResult.AreaSquareMeters, decimalPlaces: 3));
    }

    [Fact]
    public void FullWorkflow_ManualLengthSource_DoesNotNeedAutoCad()
    {
        // §18-19: 수동 입력 소스는 AutoCAD/FakeAutoCad를 전혀 거치지 않는다.
        var result = VerticalAreaCalculator.Calculate(
            sourceLengthMeters: 32.118, heightRawValue: 1.0, DrawingUnit.Meters, DateTimeOffset.Now);

        Assert.Equal(32.118, result.AreaSquareMeters, precision: 6);
    }

    [Fact]
    public async Task FullWorkflow_SelectionCancelled_PropagatesAsStructuredCancel()
    {
        // Vertical Area의 CAD 선택은 SelectLengthObjects를 그대로 쓰므로 Length의 실패 시나리오를
        // 그대로 재사용해도 충분하다 - 별도 Vertical Area 전용 Scenario가 필요 없다(§54).
        await using var fake = await FakeAutoCadProcess.StartAsync("SelectionCancelled", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.SelectionCancelled, response.Error!.Code);
    }
}
