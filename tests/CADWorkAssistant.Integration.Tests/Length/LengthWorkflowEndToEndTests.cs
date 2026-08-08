using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Length;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Length;

/// <summary>
/// Milestone 2 §54의 Headless End-to-End 흐름 그대로: FakeAutoCAD 프로세스 시작 → Connect →
/// GetDrawingContext → SelectLengthObjects → Core에서 합산/변환 → Disconnect → 프로세스 종료.
/// 전부 실제 Named Pipe로 두 개의 실제 프로세스 사이에서 일어난다. AutoCAD는 필요 없다.
/// </summary>
public class LengthWorkflowEndToEndTests
{
    [Fact]
    public async Task FullWorkflow_NormalSelection_ProducesTheKnownWorkOrderTotal()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("NormalSelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var appInfoResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetApplicationInfo, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(appInfoResponse.Success);

        var contextResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetDrawingContext, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(contextResponse.Success);
        var context = contextResponse.DeserializePayload<CADWorkAssistant.Core.Cad.DrawingContext>();
        Assert.Equal("School_Roof.dwg", context!.DocumentDisplayName);
        Assert.Equal(CADWorkAssistant.Core.Cad.DrawingUnit.Millimeters, context.Units);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(selectionResponse.Success);

        var selection = selectionResponse.DeserializePayload<LengthSelectionResponse>();
        Assert.NotNull(selection);
        Assert.Equal(3, selection!.Objects.Count);

        // Core의 집계/변환 로직으로 이어붙인다 - AutoCAD Plugin은 원본 데이터만 준다 (§25).
        var result = LengthAggregationService.Aggregate(selection, context.DocumentDisplayName, DateTimeOffset.Now);

        Assert.Equal(3, result.ObjectCount);
        Assert.NotNull(result.DisplayValueMeters);
        Assert.Equal("255.941 m", LengthFormatter.FormatMetersWithUnit(result.DisplayValueMeters!.Value));
    }

    [Fact]
    public async Task FullWorkflow_SinglePolyline_ProducesOneRow()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("SinglePolyline", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<LengthSelectionResponse>();

        Assert.Single(selection!.Objects);
        Assert.Equal(SupportedGeometryType.Polyline, selection.Objects[0].GeometryType);
    }

    [Fact]
    public async Task FullWorkflow_EmptySelection_ReturnsNoObjectsWithoutError()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("EmptySelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.True(response.Success);
        var selection = response.DeserializePayload<LengthSelectionResponse>();
        Assert.Empty(selection!.Objects);
        Assert.Empty(selection.ExcludedObjectTypeNames);
    }

    [Fact]
    public async Task FullWorkflow_MixedSupportedAndUnsupported_ReportsExclusions()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("MixedSupportedUnsupported", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<LengthSelectionResponse>();

        Assert.Equal(2, selection!.Objects.Count);
        Assert.Contains("Hatch", selection.ExcludedObjectTypeNames);
    }

    [Fact]
    public async Task FullWorkflow_UnitlessDrawing_DoesNotAutoConvert()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("UnitlessDrawing", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectLengthObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<LengthSelectionResponse>();

        var result = LengthAggregationService.Aggregate(selection!, "unitless.dwg", DateTimeOffset.Now);

        Assert.Equal(CADWorkAssistant.Core.Cad.DrawingUnit.Unitless, selection!.Unit);
        Assert.Null(result.DisplayValueMeters);
    }
}
