using CADWorkAssistant.Core.Area;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Area;

/// <summary>
/// Milestone 3 §78의 Headless End-to-End 흐름 그대로: FakeAutoCAD 프로세스 시작 → Connect →
/// GetDrawingContext → SelectAreaObjects → Core에서 분류/합산/변환 → Disconnect → 프로세스 종료.
/// Length의 LengthWorkflowEndToEndTests와 동일한 구조를 그대로 따른다 (§5). AutoCAD는 필요 없다.
/// </summary>
public class AreaWorkflowEndToEndTests
{
    [Fact]
    public async Task FullWorkflow_MultipleClosedPolylines_ProducesTheKnownWorkOrderTotal()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("MultipleClosedPolylines", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var contextResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetDrawingContext, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(contextResponse.Success);
        var context = contextResponse.DeserializePayload<CADWorkAssistant.Core.Cad.DrawingContext>();
        Assert.Equal("School_Roof.dwg", context!.DocumentDisplayName);
        Assert.Equal(CADWorkAssistant.Core.Cad.DrawingUnit.Millimeters, context.Units);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(selectionResponse.Success);

        var selection = selectionResponse.DeserializePayload<AreaSelectionResponse>();
        Assert.NotNull(selection);
        Assert.Equal(3, selection!.Objects.Count);

        // Core의 분류/합산/변환 로직으로 이어붙인다 - AutoCAD Plugin은 원본 데이터만 준다 (§5, §25).
        var result = AreaAggregationService.Aggregate(selection, context.DocumentDisplayName, DateTimeOffset.Now);

        Assert.Equal(3, result.SupportedCount);
        Assert.Equal(0, result.ExcludedCount);
        Assert.NotNull(result.DisplayValueSquareMeters);
        Assert.Equal("3,102.43 m²", AreaFormatter.FormatSquareMetersWithUnit(result.DisplayValueSquareMeters!.Value));
    }

    [Fact]
    public async Task FullWorkflow_SingleClosedPolyline_ProducesOneItem()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("SingleClosedPolyline", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        Assert.Single(selection!.Objects);
        Assert.Equal(SupportedAreaGeometryType.Polyline, selection.Objects[0].GeometryType);
        Assert.True(selection.Objects[0].IsClosed);
    }

    [Fact]
    public async Task FullWorkflow_EmptySelection_ReturnsNoObjectsWithoutError()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("EmptyAreaSelection", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.True(response.Success);
        var selection = response.DeserializePayload<AreaSelectionResponse>();
        Assert.Empty(selection!.Objects);
        Assert.Empty(selection.ExcludedObjectTypeNames);
    }

    [Fact]
    public async Task FullWorkflow_OpenPolyline_ExcludedNotZeroArea()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("OpenPolyline", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        var result = AreaAggregationService.Aggregate(selection!, "open.dwg", DateTimeOffset.Now);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.OpenItems);
    }

    [Fact]
    public async Task FullWorkflow_MixedClosedAndOpen_ReportsPartialSuccess()
    {
        // §79 Partial Success E2E: 3개 유효 + 1개 열림 → 3,102.43 m², 제외 1개
        await using var fake = await FakeAutoCadProcess.StartAsync("MixedClosedOpen", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        var result = AreaAggregationService.Aggregate(selection!, "mixed.dwg", DateTimeOffset.Now);

        Assert.Equal(4, result.SelectedCount);
        Assert.Equal(3, result.SupportedCount);
        Assert.Equal(1, result.ExcludedCount);
        Assert.Equal("3,102.43 m²", AreaFormatter.FormatSquareMetersWithUnit(result.DisplayValueSquareMeters!.Value));
    }

    [Fact]
    public async Task FullWorkflow_UnsupportedObject_ReportsExclusions()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("UnsupportedAreaObject", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        Assert.Empty(selection!.Objects);
        Assert.Contains("Hatch", selection.ExcludedObjectTypeNames);
    }

    [Fact]
    public async Task FullWorkflow_MixedSupportedAndUnsupported_ReportsBoth()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("AreaMixedSupportedUnsupported", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        Assert.Single(selection!.Objects);
        Assert.Contains("Hatch", selection.ExcludedObjectTypeNames);
    }

    [Fact]
    public async Task FullWorkflow_ZeroArea_ClassifiedAsInvalidGeometryNotValid()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("ZeroArea", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        var result = AreaAggregationService.Aggregate(selection!, "zero.dwg", DateTimeOffset.Now);

        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.InvalidGeometryItems);
    }

    [Fact]
    public async Task FullWorkflow_InvalidGeometry_ClassifiedAsInvalidGeometry()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("InvalidArea", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(response.Success);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        var result = AreaAggregationService.Aggregate(selection!, "invalid.dwg", DateTimeOffset.Now);

        Assert.Equal(0, result.SupportedCount);
        Assert.Single(result.InvalidGeometryItems);
    }

    [Fact]
    public async Task FullWorkflow_UnitlessDrawing_DoesNotAutoConvert()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("UnitlessAreaDrawing", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        var result = AreaAggregationService.Aggregate(selection!, "unitless.dwg", DateTimeOffset.Now);

        Assert.Equal(CADWorkAssistant.Core.Cad.DrawingUnit.Unitless, selection!.Unit);
        Assert.Null(result.DisplayValueSquareMeters);
    }

    [Fact]
    public async Task FullWorkflow_MeterDrawing_ConvertsWithoutScaling()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("MeterAreaDrawing", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectAreaObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = response.DeserializePayload<AreaSelectionResponse>();

        var result = AreaAggregationService.Aggregate(selection!, "meters.dwg", DateTimeOffset.Now);

        Assert.Equal(3102.43, result.DisplayValueSquareMeters!.Value, precision: 6);
    }
}
