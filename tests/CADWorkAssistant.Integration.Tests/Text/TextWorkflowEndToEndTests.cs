using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Text;

/// <summary>
/// Milestone 12 §105-108 - SelectTextObjects/AcquireTextInsertionPoint/CreateText/UpdateTextObjects
/// 전체 흐름을 실제 Named Pipe로 검증한다. FakeAutoCad는 실제 DWG를 편집하지 않는다(§95) - IPC
/// 배관과 Batch Patch Semantics(실제로 patch가 적용된 값이 돌아오는지)가 검증 대상이다. 실제
/// AutoCAD 렌더링/Undo 정확성은 Milestone 12B.
/// </summary>
public class TextWorkflowEndToEndTests
{
    [Fact]
    public async Task FullFlow_SelectTextObjects_ReturnsDBTextAndMText()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextSelectionMixed", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectTextObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.True(response.Success);
        var selection = response.DeserializePayload<TextSelectionResponse>()!;
        Assert.Equal(2, selection.Objects.Count);
        Assert.Contains(selection.Objects, o => o.EntityType == CadTextEntityType.SingleLine);
        Assert.Contains(selection.Objects, o => o.EntityType == CadTextEntityType.MultiLine);
    }

    [Fact]
    public async Task FullFlow_SelectTextObjects_ExcludesUnsupportedTypes()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextSelectionUnsupported", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectTextObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        var selection = response.DeserializePayload<TextSelectionResponse>()!;
        Assert.Single(selection.Objects);
        Assert.Contains("Dimension", selection.ExcludedObjectTypeNames);
        Assert.Contains("MLeader", selection.ExcludedObjectTypeNames);
    }

    [Fact]
    public async Task FullFlow_BatchUpdate_HeightAndColor_AppliesPatchToAllHandles()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextBatchHeight", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectTextObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = selectionResponse.DeserializePayload<TextSelectionResponse>()!;
        var handles = selection.Objects.Select(o => o.Handle).ToList();

        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(),
            OptionalValue<double>.Some(300),
            OptionalValue<string>.None(),
            OptionalValue<CadColorDto>.Some(CadColorPalette.ByLayer));

        var updateResponse = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(handles, patch),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.True(updateResponse.Success);
        var result = updateResponse.DeserializePayload<UpdateTextObjectsResponse>()!;
        Assert.Equal(handles.Count, result.UpdatedCount);
        Assert.All(result.UpdatedObjects, o => Assert.Equal(300, o.Height));
        Assert.All(result.UpdatedObjects, o => Assert.Equal(CadColorPalette.ByLayer, o.Color));

        // §157: 지정하지 않은 속성(Content)은 원본 그대로 보존돼야 한다.
        var original = selection.Objects.First(o => o.Handle == result.UpdatedObjects[0].Handle);
        Assert.Equal(original.Content, result.UpdatedObjects[0].Content);
    }

    [Fact]
    public async Task FullFlow_SingleContentEdit_OnlyChangesContent()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextUpdateSingle", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectTextObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var selection = selectionResponse.DeserializePayload<TextSelectionResponse>()!;
        var original = Assert.Single(selection.Objects);

        var patch = new TextUpdatePatch(
            OptionalValue<string>.Some("수정된 내용"),
            OptionalValue<double>.None(),
            OptionalValue<string>.None(),
            OptionalValue<CadColorDto>.None());

        var updateResponse = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(new[] { original.Handle }, patch),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        var result = updateResponse.DeserializePayload<UpdateTextObjectsResponse>()!;
        var updated = Assert.Single(result.UpdatedObjects);
        Assert.Equal("수정된 내용", updated.Content);
        Assert.Equal(original.Height, updated.Height); // 변경하지 않은 속성은 유지
        Assert.Equal(original.LayerName, updated.LayerName);
    }

    [Fact]
    public async Task FullFlow_AcquirePointThenCreateDBText_ReturnsCreatedObject()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextCreateDbText", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var pointResponse = await client.SendRequestAsync(
            IpcMessageTypes.AcquireTextInsertionPoint, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(pointResponse.Success);
        var point = pointResponse.DeserializePayload<AcquireTextInsertionPointResponse>()!.Point;

        var createResponse = await client.SendRequestAsync(
            IpcMessageTypes.CreateText,
            new CreateTextRequest(CadTextEntityType.SingleLine, "신규 문자", 250, "A-TEXT", null, point),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.True(createResponse.Success);
        var created = createResponse.DeserializePayload<CreateTextResponse>()!.Created;
        Assert.Equal("신규 문자", created.Content);
        Assert.Equal(250, created.Height);
        Assert.Equal(CadTextEntityType.SingleLine, created.EntityType);
    }

    [Fact]
    public async Task FullFlow_CreateMText_DefaultsToByLayerColor_WhenColorNotSpecified()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextCreateMText", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var pointResponse = await client.SendRequestAsync(
            IpcMessageTypes.AcquireTextInsertionPoint, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var point = pointResponse.DeserializePayload<AcquireTextInsertionPointResponse>()!.Point;

        var createResponse = await client.SendRequestAsync(
            IpcMessageTypes.CreateText,
            new CreateTextRequest(CadTextEntityType.MultiLine, "여러행 신규 문자", 180, null, null, point),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        var created = createResponse.DeserializePayload<CreateTextResponse>()!.Created;
        Assert.Equal(CadTextEntityType.MultiLine, created.EntityType);
        Assert.Equal(CadColorPalette.ByLayer, created.Color);
    }
}
