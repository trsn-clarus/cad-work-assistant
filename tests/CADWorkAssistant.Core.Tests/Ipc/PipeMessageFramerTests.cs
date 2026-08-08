using CADWorkAssistant.Infrastructure.Ipc;

namespace CADWorkAssistant.Core.Tests.Ipc;

public class PipeMessageFramerTests
{
    [Fact]
    public async Task WriteThenRead_RoundTripsExactMessage()
    {
        using var stream = new MemoryStream();

        await PipeMessageFramer.WriteMessageAsync(stream, "{\"hello\":\"world\"}", CancellationToken.None);
        stream.Position = 0;

        var message = await PipeMessageFramer.ReadMessageAsync(stream, maxMessageSizeBytes: 1024, CancellationToken.None);

        Assert.Equal("{\"hello\":\"world\"}", message);
    }

    [Fact]
    public async Task Read_OnEmptyStream_ReturnsNullInsteadOfThrowing()
    {
        using var stream = new MemoryStream();

        var message = await PipeMessageFramer.ReadMessageAsync(stream, maxMessageSizeBytes: 1024, CancellationToken.None);

        Assert.Null(message);
    }

    [Fact]
    public async Task Read_MultipleFramedMessages_ReturnsThemInOrder()
    {
        using var stream = new MemoryStream();
        await PipeMessageFramer.WriteMessageAsync(stream, "first", CancellationToken.None);
        await PipeMessageFramer.WriteMessageAsync(stream, "second", CancellationToken.None);
        stream.Position = 0;

        var first = await PipeMessageFramer.ReadMessageAsync(stream, 1024, CancellationToken.None);
        var second = await PipeMessageFramer.ReadMessageAsync(stream, 1024, CancellationToken.None);

        Assert.Equal("first", first);
        Assert.Equal("second", second);
    }

    [Fact]
    public async Task Read_LengthPrefixExceedsMax_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        await stream.WriteAsync(BitConverter.GetBytes(2048), CancellationToken.None);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => PipeMessageFramer.ReadMessageAsync(stream, maxMessageSizeBytes: 1024, CancellationToken.None));
    }

    [Fact]
    public async Task Read_StreamTruncatedMidPayload_ThrowsEndOfStream()
    {
        using var stream = new MemoryStream();
        // 길이는 10바이트라고 선언하지만 실제로는 3바이트만 쓴다.
        await stream.WriteAsync(BitConverter.GetBytes(10), CancellationToken.None);
        await stream.WriteAsync("abc"u8.ToArray(), CancellationToken.None);
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => PipeMessageFramer.ReadMessageAsync(stream, maxMessageSizeBytes: 1024, CancellationToken.None));
    }

    [Fact]
    public async Task WriteThenRead_HandlesEmptyMessage()
    {
        using var stream = new MemoryStream();
        await PipeMessageFramer.WriteMessageAsync(stream, string.Empty, CancellationToken.None);
        stream.Position = 0;

        var message = await PipeMessageFramer.ReadMessageAsync(stream, 1024, CancellationToken.None);

        Assert.Equal(string.Empty, message);
    }
}
