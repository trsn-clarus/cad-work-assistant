using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CADWorkAssistant.Core.Ipc;

/// <summary>
/// 하나의 MessageType을 처리하는 Handler. AutoCAD Plugin이 구현체를 제공한다 (거대한 switch 대신, §39).
/// </summary>
public interface IIpcRequestHandler
{
    string MessageType { get; }

    Task<IpcHandlerResult> HandleAsync(JsonElement? payload, CancellationToken cancellationToken);
}
