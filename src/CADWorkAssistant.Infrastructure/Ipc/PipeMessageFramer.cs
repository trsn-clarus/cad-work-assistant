using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CADWorkAssistant.Infrastructure.Ipc;

/// <summary>
/// Named Pipe는 Stream이라 "한 번의 Read = 메시지 하나"가 보장되지 않는다 (§15).
/// 4-byte length prefix + UTF-8 JSON으로 메시지를 구분한다. Desktop(net8)과 AutoCAD Plugin(net48)
/// 양쪽에서 동일하게 쓰기 위해 Infrastructure에 둔다 - AutoCAD/WPF 어느 쪽에도 의존하지 않는다.
/// 이 프로세스는 항상 같은 Windows 머신에서만 통신하므로 BitConverter의 native(리틀 엔디언) 그대로 사용한다.
/// </summary>
public static class PipeMessageFramer
{
    public static async Task WriteMessageAsync(Stream stream, string message, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        var lengthPrefix = BitConverter.GetBytes(payload.Length);

        await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>다음 메시지를 읽는다. 상대방이 정상적으로 연결을 끊었으면 null을 반환한다 (예외가 아님).</summary>
    public static async Task<string?> ReadMessageAsync(Stream stream, int maxMessageSizeBytes, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        var gotLength = await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        if (!gotLength)
        {
            return null;
        }

        var length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length < 0 || length > maxMessageSizeBytes)
        {
            throw new InvalidDataException($"IPC message length {length} is outside the allowed range (0..{maxMessageSizeBytes}).");
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var payloadBuffer = new byte[length];
        var gotPayload = await ReadExactAsync(stream, payloadBuffer, cancellationToken).ConfigureAwait(false);
        if (!gotPayload)
        {
            throw new EndOfStreamException("IPC stream closed while reading a message payload.");
        }

        return Encoding.UTF8.GetString(payloadBuffer);
    }

    /// <summary>buffer를 가득 채울 때까지 읽는다. 시작하자마자 EOF면 false, 읽는 도중 EOF면 예외.</summary>
    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (totalRead == 0)
                {
                    return false;
                }

                throw new EndOfStreamException("IPC stream closed mid-message.");
            }

            totalRead += read;
        }

        return true;
    }
}
