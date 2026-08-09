using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Core.Text;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Text;

/// <summary>Milestone 12 §107, §109 - Cancel/Disconnect/Invalid handle/Locked layer/오류가 전부
/// 구조화된 결과로 와야 한다 - DrawingSelectionFailureTests/PlotFailureTests와 같은 패턴. Batch
/// Atomicity(§53, §109)도 여기서 검증한다 - 실패하면 부분 성공이 아니라 명확한 오류 하나여야 한다.</summary>
public class TextFailureTests
{
    [Fact]
    public async Task SelectTextObjects_Cancelled_ReturnsStructuredCancelNotAnException()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextSelectionCancelled", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.SelectTextObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.SelectionCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task AcquireTextInsertionPoint_Cancelled_DoesNotCreateText()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextCreateCancelled", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.AcquireTextInsertionPoint, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.SelectionCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task SelectTextObjects_Disconnect_ClientObservesFailureInsteadOfHanging()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextDisconnected", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendRequestAsync(
            IpcMessageTypes.SelectTextObjects, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateTextObjects_InvalidHandle_FailsEntireBatch()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextUpdateInvalidHandle", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(), OptionalValue<double>.Some(300),
            OptionalValue<string>.None(), OptionalValue<CadColorDto>.None());

        var response = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(new[] { "DEAD01", "DEAD02" }, patch),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task UpdateTextObjects_LockedLayer_FailsWithoutPartialSuccess()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextUpdateLocked", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(), OptionalValue<double>.Some(300),
            OptionalValue<string>.None(), OptionalValue<CadColorDto>.None());

        var response = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(new[] { "8D01" }, patch),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        // InvalidHandle과 같은 분류(InvalidRequest) - 잠긴 Layer는 raw AutoCAD 예외가 아니라
        // 사용자가 이해하고 대응할 수 있는 business-rule 실패이므로 Desktop이 메시지를 그대로
        // 보여줄 수 있는 코드로 분류한다(§55).
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task UpdateTextObjects_SimulatedAutoCadError_ReturnsStructuredError()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextUpdateError", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(), OptionalValue<double>.Some(300),
            OptionalValue<string>.None(), OptionalValue<CadColorDto>.None());

        var response = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(new[] { "8A01" }, patch),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.ApiExecutionFailed, response.Error!.Code);
    }

    [Fact]
    public async Task UpdateTextObjects_NoHandles_FailsWithInvalidRequest()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextUpdateSingle", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(), OptionalValue<double>.Some(300),
            OptionalValue<string>.None(), OptionalValue<CadColorDto>.None());

        var response = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(Array.Empty<string>(), patch),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task UpdateTextObjects_EmptyPatch_FailsWithInvalidRequest()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextUpdateSingle", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(new[] { "8A01" }, TextUpdatePatch.Empty()),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task UpdateTextObjects_NonPositiveHeight_FailsWithInvalidRequest()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextUpdateSingle", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var patch = new TextUpdatePatch(
            OptionalValue<string>.None(), OptionalValue<double>.Some(0),
            OptionalValue<string>.None(), OptionalValue<CadColorDto>.None());

        var response = await client.SendRequestAsync(
            IpcMessageTypes.UpdateTextObjects,
            new UpdateTextObjectsRequest(new[] { "8A01" }, patch),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task CreateText_EmptyContent_FailsWithInvalidRequest()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextCreateDbText", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.CreateText,
            new CreateTextRequest(CadTextEntityType.SingleLine, "   ", 250, null, null, new CadPointDto(0, 0, 0)),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task CreateText_NonPositiveHeight_FailsWithInvalidRequest()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("TextCreateDbText", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.CreateText,
            new CreateTextRequest(CadTextEntityType.SingleLine, "내용", -5, null, null, new CadPointDto(0, 0, 0)),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }
}
